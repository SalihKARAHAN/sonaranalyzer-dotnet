﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2015 SonarSource
 * sonarqube@googlegroups.com
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
 * You should have received a copy of the GNU Lesser General Public
 * License along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02
 */

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarLint.Common;
using SonarLint.Helpers;

namespace SonarLint.Rules.Common
{
    public abstract class FlagsEnumWithoutInitializerBase : MultiLanguageDiagnosticAnalyzer
    {
        protected const string DiagnosticId = "S2345";
        protected const string Title = "Flags enumerations should explicitly initialize all their members";
        protected const string Description =
            "Flags enumerations should not rely on the language to initialize the values of their members. Implicit initialization will set " +
            "the first member to 0, and increment the value by one for each subsequent member. This implicit behavior does not allow members " +
            "to be combined using the bitwise or operator. Instead, powers of two, i.e. 1, 2, 4, 8, 16, etc. should be used to explicitly " +
            "initialize all the members.";
        protected const string MessageFormat = "Initialize all the members of this enumeration.";
        protected const string Category = Constants.SonarLint;
        protected const Severity RuleSeverity = Severity.Critical;
        protected const bool IsActivatedByDefault = true;

        protected static readonly DiagnosticDescriptor Rule =
            new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category,
                RuleSeverity.ToDiagnosticSeverity(), IsActivatedByDefault,
                helpLinkUri: DiagnosticId.GetHelpLink(),
                description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        internal static bool HasFlagsAttribute(SyntaxNode node, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetDeclaredSymbol(node);
            if (symbol == null)
            {
                return false;
            }

            var flagsAttribute = symbol.GetAttributes().FirstOrDefault(attribute =>
            {
                var type = attribute.AttributeClass;
                return type != null &&
                    type.ToDisplayString() == "System.FlagsAttribute";
            });
            return flagsAttribute != null;
        }
    }

    public abstract class FlagsEnumWithoutInitializerBase<TLanguageKindEnum, TEnumDeclarationSyntax> : FlagsEnumWithoutInitializerBase
        where TLanguageKindEnum : struct
        where TEnumDeclarationSyntax : SyntaxNode
    {
        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeActionInNonGenerated(
                GeneratedCodeRecognizer,
                c =>
                {
                    var enumDeclaration = (TEnumDeclarationSyntax)c.Node;
                    var hasFlagsAttribute = HasFlagsAttribute(enumDeclaration, c.SemanticModel);
                    if (!hasFlagsAttribute)
                    {
                        return;
                    }

                    if (!AllMembersAreInitialized(enumDeclaration))
                    {
                        c.ReportDiagnostic(Diagnostic.Create(Rule, GetIdentifier(enumDeclaration).GetLocation()));
                    }
                },
                SyntaxKindsOfInterest.ToArray());
        }

        public abstract ImmutableArray<TLanguageKindEnum> SyntaxKindsOfInterest { get; }
        protected abstract SyntaxToken GetIdentifier(TEnumDeclarationSyntax declaration);
        protected abstract bool AllMembersAreInitialized(TEnumDeclarationSyntax declaration);
    }
}
