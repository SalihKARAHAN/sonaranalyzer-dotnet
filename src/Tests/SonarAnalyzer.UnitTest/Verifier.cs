/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2017 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using FluentAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.Helpers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CS = Microsoft.CodeAnalysis.CSharp;
using VB = Microsoft.CodeAnalysis.VisualBasic;
using System;
using SonarAnalyzer.UnitTest.TestFramework;

namespace SonarAnalyzer.UnitTest
{
    internal static class Verifier
    {
        private static readonly MetadataReference systemAssembly = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        private static readonly MetadataReference systemLinqAssembly = MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location);
        private static readonly MetadataReference systemNetAssembly = MetadataReference.CreateFromFile(typeof(WebClient).Assembly.Location);

        private const string EXPECTED_ISSUE_LOCATION_PATTERN = @"\s*(\^+)\s*";
        private const string COMMENT_START_CSHARP = @"//";
        private const string COMMENT_START_VBNET = @"'";

        private const string EXPECTED_ISSUE_LOCATION_PATTERN_CSHARP = @"^" + COMMENT_START_CSHARP + EXPECTED_ISSUE_LOCATION_PATTERN + "$";
        private const string EXPECTED_ISSUE_LOCATION_PATTERN_VBNET = @"^" + COMMENT_START_VBNET + EXPECTED_ISSUE_LOCATION_PATTERN + "$";
        private const string NONCOMPLIANT_START = "Noncompliant";
        private const string FIXED_MESSAGE = "Fixed";
        private const string NONCOMPLIANT_PATTERN = NONCOMPLIANT_START + @".*";
        private const string NONCOMPLIANT_LINE_PATTERN = NONCOMPLIANT_START + @"@([\+|-]?)([0-9]+)";
        private const string NONCOMPLIANT_MESSAGE_PATTERN = NONCOMPLIANT_START + @".*({{.*}}).*";

        private const string GeneratedAssemblyName = "foo";
        private const string TestAssemblyName = "fooTest";
        private const string AnalyzerFailedDiagnosticId = "AD0001";
        private const string CSharpFileExtension = ".cs";

        #region Verify*

        public static void VerifyNoExceptionThrown(string path,
            IEnumerable<DiagnosticAnalyzer> diagnosticAnalyzers)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(path, GeneratedAssemblyName, workspace);
                var compilation = document.Project.GetCompilationAsync().Result;
                var diagnostics = GetAllDiagnostics(compilation, diagnosticAnalyzers);
                VerifyNoExceptionThrown(diagnostics);
            }
        }

        public static void VerifyAnalyzer(string path, SonarDiagnosticAnalyzer diagnosticAnalyzer, ParseOptions options = null,
            params MetadataReference[] additionalReferences)
        {
            var file = new FileInfo(path);
            var parseOptions = GetParseOptionsAlternatives(options, file.Extension);

            var issueLocationCollector = new IssueLocationCollector();

            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(file, GeneratedAssemblyName, workspace, additionalReferences: additionalReferences);
                var project = document.Project;

                foreach (var parseOption in parseOptions)
                {
                    if (parseOption != null)
                    {
                        project = project.WithParseOptions(parseOption);
                    }

                    var compilation = project.GetCompilationAsync().Result;
                    var diagnostics = GetDiagnostics(compilation, diagnosticAnalyzer);
                    var expectedIssues = issueLocationCollector
                        .GetExpectedIssueLocations(compilation)
                        .ToDictionary(l => l.LineNumber, l => l);

                    var language = file.Extension == CSharpFileExtension
                        ? LanguageNames.CSharp
                        : LanguageNames.VisualBasic;
                    var exactLocations = GetExactIssueLocations(compilation.SyntaxTrees.First(), language);

                    foreach (var diagnostic in diagnostics)
                    {
                        var line = diagnostic.GetLineNumberToReport();
                        if (!expectedIssues.ContainsKey(line))
                        {
                            Assert.Fail($"Issue not expected on line {line}");
                        }

                        var expectedMessage = expectedIssues[line];
                        if (expectedMessage.Message != null)
                        {
                            var message = diagnostic.GetMessage();

                            Assert.AreEqual(expectedMessage.Message, message,
                                $"Message does not match expected on line {line}");
                        }

                        if (exactLocations.ContainsKey(line))
                        {
                            var expectedLocation = exactLocations[line];

                            var diagnosticStart = diagnostic.Location.GetLineSpan().StartLinePosition.Character;
                            Assert.AreEqual(expectedLocation.StartPosition, diagnosticStart,
                                $"The start position of the diagnostic ({diagnosticStart}) doesn't match " +
                                $"the expected value ({expectedLocation.StartPosition}) in line {line}.");

                            Assert.AreEqual(expectedLocation.Length, diagnostic.Location.SourceSpan.Length,
                                $"The length of the diagnostic ({diagnostic.Location.SourceSpan.Length}) doesn't match " +
                                $"the expected value ({expectedLocation.Length}) in line {line}.");

                            exactLocations.Remove(line);
                        }

                        expectedIssues.Remove(line);
                    }

                    exactLocations.Should().BeEmpty($"Issue expected but not found on line(s) {string.Join(",", exactLocations.Keys)}");
                    Assert.AreEqual(0, expectedIssues.Count, $"Issue not expected but found on line(s) {string.Join(",", expectedIssues.Keys)}.");
                }
            }
        }

        public static void VerifyNoIssueReportedInTest(string path, SonarDiagnosticAnalyzer diagnosticAnalyzer)
        {
            VerifyNoIssueReported(path, TestAssemblyName, diagnosticAnalyzer);
        }

        public static void VerifyNoIssueReported(string path, SonarDiagnosticAnalyzer diagnosticAnalyzer)
        {
            VerifyNoIssueReported(path, GeneratedAssemblyName, diagnosticAnalyzer);
        }

        public static void VerifyCodeFix(string path, string pathToExpected, SonarDiagnosticAnalyzer diagnosticAnalyzer,
            SonarCodeFixProvider codeFixProvider)
        {
            VerifyCodeFix(path, pathToExpected, pathToExpected, diagnosticAnalyzer, codeFixProvider, null);
        }

        public static void VerifyCodeFix(string path, string pathToExpected, string pathToBatchExpected, SonarDiagnosticAnalyzer diagnosticAnalyzer,
            SonarCodeFixProvider codeFixProvider)
        {
            VerifyCodeFix(path, pathToExpected, pathToBatchExpected, diagnosticAnalyzer, codeFixProvider, null);
        }

        public static void VerifyCodeFix(string path, string pathToExpected, SonarDiagnosticAnalyzer diagnosticAnalyzer,
            SonarCodeFixProvider codeFixProvider, string codeFixTitle)
        {
            VerifyCodeFix(path, pathToExpected, pathToExpected, diagnosticAnalyzer, codeFixProvider, codeFixTitle);
        }

        public static void VerifyCodeFix(string path, string pathToExpected, string pathToBatchExpected, SonarDiagnosticAnalyzer diagnosticAnalyzer,
            SonarCodeFixProvider codeFixProvider, string codeFixTitle)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var file = new FileInfo(path);
                var parseOptions = GetParseOptionsWithDifferentLanguageVersions(null, file.Extension);

                foreach (var parseOption in parseOptions)
                {
                    var document = GetDocument(file, GeneratedAssemblyName, workspace, removeAnalysisComments: true);
                    RunCodeFixWhileDocumentChanges(diagnosticAnalyzer, codeFixProvider, codeFixTitle, document, parseOption, pathToExpected);
                }
            }

            VerifyFixAllCodeFix(path, pathToBatchExpected, diagnosticAnalyzer, codeFixProvider, codeFixTitle);
        }


        #endregion

        #region Generic helper

        private static void VerifyNoIssueReported(string path, string assemblyName, DiagnosticAnalyzer diagnosticAnalyzer)
        {
            using (var workspace = new AdhocWorkspace())
            {
                var document = GetDocument(path, assemblyName, workspace);
                var compilation = document.Project.GetCompilationAsync().Result;
                var diagnostics = GetDiagnostics(compilation, diagnosticAnalyzer);

                diagnostics.Should().HaveCount(0);
            }
        }

        private static void VerifyFixAllCodeFix(string path, string pathToExpected, DiagnosticAnalyzer diagnosticAnalyzer,
            CodeFixProvider codeFixProvider, string codeFixTitle)
        {
            var fixAllProvider = codeFixProvider.GetFixAllProvider();
            if (fixAllProvider == null)
            {
                return;
            }

            using (var workspace = new AdhocWorkspace())
            {
                var file = new FileInfo(path);
                var parseOptions = GetParseOptionsWithDifferentLanguageVersions(null, file.Extension);

                foreach (var parseOption in parseOptions)
                {
                    var document = GetDocument(file, GeneratedAssemblyName, workspace, removeAnalysisComments: true);
                    RunFixAllProvider(diagnosticAnalyzer, codeFixProvider, codeFixTitle, fixAllProvider, document, parseOption, pathToExpected);
                }
            }
        }

        private static Document GetDocument(string filePath, string assemblyName,
            AdhocWorkspace workspace, bool removeAnalysisComments = false, params MetadataReference[] additionalReferences)
        {
            var file = new FileInfo(filePath);
            return GetDocument(file, assemblyName, workspace, removeAnalysisComments, additionalReferences);
        }

        private static Document GetDocument(FileInfo file, string assemblyName,
            AdhocWorkspace workspace, bool removeAnalysisComments = false, params MetadataReference[] additionalReferences)
        {
            var language = file.Extension == CSharpFileExtension
                ? LanguageNames.CSharp
                : LanguageNames.VisualBasic;

            var locationPattern = file.Extension == CSharpFileExtension
                ? EXPECTED_ISSUE_LOCATION_PATTERN_CSHARP
                : EXPECTED_ISSUE_LOCATION_PATTERN_VBNET;

            var lines = File.ReadAllText(file.FullName, Encoding.UTF8)
                .Split(new[] { Environment.NewLine}, StringSplitOptions.None);

            if (removeAnalysisComments)
            {
                lines = lines.Where(l => !Regex.IsMatch(l, locationPattern)).ToArray();
                lines = lines.Select(l => {
                    var match = Regex.Match(l, NONCOMPLIANT_PATTERN);
                    return match.Success ? l.Replace(match.Groups[0].Value, FIXED_MESSAGE) : l;
                }).ToArray();

            }

            var text = string.Join(Environment.NewLine, lines);

            var document = workspace.CurrentSolution.AddProject(assemblyName,
                    $"{assemblyName}.dll", language)
                .AddMetadataReference(systemAssembly)
                .AddMetadataReference(systemLinqAssembly)
                .AddMetadataReference(systemNetAssembly)
                .AddMetadataReferences(additionalReferences)
                .AddDocument(file.Name, text);

            // adding an extra file to the project
            // this won't trigger any issues, but it keeps a reference to the original ParseOption, so
            // if an analyzer/codefix changes the language version, Roslyn throws an ArgumentException
            document = document.Project.AddDocument("ExtraEmptyFile.g" + file.Extension, string.Empty);

            return document.Project.Documents.Single(d => d.Name == file.Name);
        }

        #endregion

        #region Analyzer helpers

        private static IEnumerable<ParseOptions> GetParseOptionsAlternatives(ParseOptions options, string fileExtension)
        {
            return GetParseOptionsWithDifferentLanguageVersions(options, fileExtension).Concat(new[] { options });
        }

        private static IEnumerable<ParseOptions> GetParseOptionsWithDifferentLanguageVersions(ParseOptions options, string fileExtension)
        {
            if (fileExtension == CSharpFileExtension)
            {
                if (options == null)
                {
                    var csOptions = new CS.CSharpParseOptions();
                    yield return csOptions.WithLanguageVersion(CS.LanguageVersion.CSharp6);
                    yield return csOptions.WithLanguageVersion(CS.LanguageVersion.CSharp5);
                }
                yield break;
            }

            var vbOptions = options as VB.VisualBasicParseOptions ?? new VB.VisualBasicParseOptions();
            yield return vbOptions.WithLanguageVersion(VB.LanguageVersion.VisualBasic14);
            yield return vbOptions.WithLanguageVersion(VB.LanguageVersion.VisualBasic12);
        }

        internal static IEnumerable<Diagnostic> GetDiagnostics(Compilation compilation,
            DiagnosticAnalyzer diagnosticAnalyzer)
        {
            var id = diagnosticAnalyzer.SupportedDiagnostics.Single().Id;

            var diagnostics = GetAllDiagnostics(compilation, new[] { diagnosticAnalyzer }).ToList();
            VerifyNoExceptionThrown(diagnostics);

            return diagnostics.Where(d => id == d.Id);
        }

        private static void VerifyNoExceptionThrown(IEnumerable<Diagnostic> diagnostics)
        {
            diagnostics.Where(d => d.Id == AnalyzerFailedDiagnosticId).Should().BeEmpty();
        }

        private static IEnumerable<Diagnostic> GetAllDiagnostics(Compilation compilation,
            IEnumerable<DiagnosticAnalyzer> diagnosticAnalyzers)
        {
            using (var tokenSource = new CancellationTokenSource())
            {
                var compilationOptions = compilation.Language == LanguageNames.CSharp
                    ? (CompilationOptions)new CS.CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true)
                    : new VB.VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
                var supportedDiagnostics = diagnosticAnalyzers
                        .SelectMany(analyzer => analyzer.SupportedDiagnostics)
                        .ToList();
                compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                    supportedDiagnostics
                        .Select(diagnostic =>
                            new KeyValuePair<string, ReportDiagnostic>(diagnostic.Id, ReportDiagnostic.Warn))
                        .Union(
                            new[]
                            {
                                new KeyValuePair<string, ReportDiagnostic>(AnalyzerFailedDiagnosticId, ReportDiagnostic.Error)
                            }));

                var compilationWithOptions = compilation.WithOptions(compilationOptions);
                var compilationWithAnalyzer = compilationWithOptions
                    .WithAnalyzers(
                        diagnosticAnalyzers.ToImmutableArray(),
                        cancellationToken: tokenSource.Token);

                return compilationWithAnalyzer.GetAllDiagnosticsAsync().Result;
            }
        }

        private static Dictionary<int, ExactIssueLocation> GetExactIssueLocations(SyntaxTree syntaxTree, string language)
        {
            var exactLocations = new Dictionary<int, ExactIssueLocation>();

            var locationPattern = language == LanguageNames.CSharp
                ? EXPECTED_ISSUE_LOCATION_PATTERN_CSHARP
                : EXPECTED_ISSUE_LOCATION_PATTERN_VBNET;

            foreach (var line in syntaxTree.GetText().Lines)
            {
                var lineText = line.ToString();
                var match = Regex.Match(lineText, locationPattern);
                if (!match.Success)
                {
                    var commentStart = language == LanguageNames.CSharp ? COMMENT_START_CSHARP : COMMENT_START_VBNET;
                    if (Regex.IsMatch(lineText, commentStart + EXPECTED_ISSUE_LOCATION_PATTERN + "$"))
                    {
                        Assert.Fail("Line matches expected location pattern, but doesn't start at the beginning of the line.");
                    }

                    continue;
                }

                var position = match.Groups[1];
                exactLocations.Add(line.LineNumber, new ExactIssueLocation
                {
                    Line = line.LineNumber,
                    StartPosition = position.Index,
                    Length = position.Length,
                });
            }

            return exactLocations;
        }

        #endregion

        #region Codefix helper

        private static void RunCodeFixWhileDocumentChanges(DiagnosticAnalyzer diagnosticAnalyzer, CodeFixProvider codeFixProvider,
            string codeFixTitle, Document document, ParseOptions parseOption, string pathToExpected)
        {
            var currentDocument = document;
            List<Diagnostic> diagnostics;
            string actualCode;
            CalculateDiagnosticsAndCode(diagnosticAnalyzer, currentDocument, parseOption, out diagnostics, out actualCode);

            Assert.AreNotEqual(0, diagnostics.Count);

            string codeBeforeFix;
            var codeFixExecutedAtLeastOnce = false;

            do
            {
                codeBeforeFix = actualCode;

                var codeFixExecuted = false;
                for (int diagnosticIndexToFix = 0; !codeFixExecuted && diagnosticIndexToFix < diagnostics.Count; diagnosticIndexToFix++)
                {
                    var codeActionsForDiagnostic = GetCodeActionsForDiagnostic(codeFixProvider, currentDocument, diagnostics[diagnosticIndexToFix]);

                    CodeAction codeActionToExecute;
                    if (TryGetCodeActionToApply(codeFixTitle, codeActionsForDiagnostic, out codeActionToExecute))
                    {
                        currentDocument = ApplyCodeFix(currentDocument, codeActionToExecute);
                        CalculateDiagnosticsAndCode(diagnosticAnalyzer, currentDocument, parseOption, out diagnostics, out actualCode);

                        codeFixExecutedAtLeastOnce = true;
                        codeFixExecuted = true;
                    }
                }
            } while (codeBeforeFix != actualCode);

            Assert.IsTrue(codeFixExecutedAtLeastOnce);
            Assert.AreEqual(File.ReadAllText(pathToExpected), actualCode);
        }

        private static void RunFixAllProvider(DiagnosticAnalyzer diagnosticAnalyzer, CodeFixProvider codeFixProvider,
            string codeFixTitle, FixAllProvider fixAllProvider, Document document, ParseOptions parseOption, string pathToExpected)
        {
            var currentDocument = document;
            List<Diagnostic> diagnostics;
            string actualCode;
            CalculateDiagnosticsAndCode(diagnosticAnalyzer, currentDocument, parseOption, out diagnostics, out actualCode);

            Assert.AreNotEqual(0, diagnostics.Count);

            var fixAllDiagnosticProvider = new FixAllDiagnosticProvider(
                codeFixProvider.FixableDiagnosticIds.ToImmutableHashSet(),
                (doc, ids, ct) => Task.FromResult(
                    GetDiagnostics(currentDocument.Project.GetCompilationAsync(ct).Result, diagnosticAnalyzer)),
                null);
            var fixAllContext = new FixAllContext(currentDocument, codeFixProvider, FixAllScope.Document,
                codeFixTitle,
                codeFixProvider.FixableDiagnosticIds,
                fixAllDiagnosticProvider,
                CancellationToken.None);
            var codeActionToExecute = fixAllProvider.GetFixAsync(fixAllContext).Result;

            Assert.IsNotNull(codeActionToExecute);

            currentDocument = ApplyCodeFix(currentDocument, codeActionToExecute);

            CalculateDiagnosticsAndCode(diagnosticAnalyzer, currentDocument, parseOption, out diagnostics, out actualCode);
            Assert.AreEqual(File.ReadAllText(pathToExpected), actualCode);
        }

        private static void CalculateDiagnosticsAndCode(DiagnosticAnalyzer diagnosticAnalyzer, Document document, ParseOptions parseOption,
            out List<Diagnostic> diagnostics,
            out string actualCode)
        {
            var project = document.Project;
            if (parseOption != null)
            {
                project = project.WithParseOptions(parseOption);
            }

            diagnostics = GetDiagnostics(project.GetCompilationAsync().Result, diagnosticAnalyzer).ToList();
            actualCode = document.GetSyntaxRootAsync().Result.GetText().ToString();
        }

        private static Document ApplyCodeFix(Document document, CodeAction codeAction)
        {
            var operations = codeAction.GetOperationsAsync(CancellationToken.None).Result;
            var solution = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution;
            return solution.GetDocument(document.Id);
        }

        private static bool TryGetCodeActionToApply(string codeFixTitle, IEnumerable<CodeAction> codeActions,
            out CodeAction codeAction)
        {
            codeAction = codeFixTitle != null
                ? codeActions.SingleOrDefault(action => action.Title == codeFixTitle)
                : codeActions.FirstOrDefault();

            return codeAction != null;
        }

        private static IEnumerable<CodeAction> GetCodeActionsForDiagnostic(CodeFixProvider codeFixProvider, Document document,
            Diagnostic diagnostic)
        {
            var actions = new List<CodeAction>();
            var context = new CodeFixContext(document, diagnostic, (a, d) => actions.Add(a), CancellationToken.None);

            codeFixProvider.RegisterCodeFixesAsync(context).Wait();
            return actions;
        }

        #endregion

        private class ExactIssueLocation
        {
            public int Line { get; set; }
            public int StartPosition { get; set; }
            public int Length { get; set; }
        }
    }
}
