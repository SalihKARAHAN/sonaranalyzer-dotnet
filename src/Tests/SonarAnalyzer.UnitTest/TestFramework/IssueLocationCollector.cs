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

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace SonarAnalyzer.UnitTest.TestFramework
{
    public class IssueLocationCollector
    {
        private const string OFFSET_PATTERN = @"(?:@(?<sign>[\+|-]?)(?<offset>[0-9]+))?";
        private const string FLOWS_PATTERN = @"\s*(\[(?<flows>.*)\])*";
        private const string MESSAGE_PATTERN = @"\s*(\{\{(?<message>.*)\}\})*";
        private const string NONCOMPLIANT_PATTERN = @"(?<issue>Noncompliant|Flow)" + OFFSET_PATTERN + FLOWS_PATTERN + MESSAGE_PATTERN;

        public IEnumerable<IIssueLocation> GetIssueLocations(TextLine line)
        {
            var lineString = line.ToString();

            var match = Regex.Match(lineString, NONCOMPLIANT_PATTERN);
            if (!match.Success)
            {
                yield break;
            }

            var isPrimary = GetIsPrimary(match);
            var lineNumber = line.LineNumber + 1 + GetOffset(match);
            var message = GetMessage(match);
            var flowIds = GetFlowIds(match);

            foreach (var flowId in flowIds)
            {
                yield return new IssueLocation
                {
                    IsPrimary = isPrimary,
                    LineNumber = lineNumber,
                    Message = message,
                    FlowId = flowId,
                };
            }
        }

        public IEnumerable<IIssueLocation> GetExpectedIssueLocations(Compilation compilation)
        {
            var lines = compilation.SyntaxTrees.First().GetText().Lines;

            return lines.SelectMany(GetIssueLocations)
                .Where(i => i != null);
        }

        private bool GetIsPrimary(Match match)
        {
            return match.Groups["issue"].Value == "Noncompliant";
        }

        private static string GetMessage(Match match)
        {
            var message = match.Groups["message"];
            return string.IsNullOrWhiteSpace(message.Value) ? null : message.Value;
        }

        private static IEnumerable<string> GetFlowIds(Match match)
        {
            var flows = match.Groups["flows"].Value;
            if (string.IsNullOrWhiteSpace(flows))
            {
                return new string[] { null };
            }

            return flows.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s));
        }

        private static int GetOffset(Match match)
        {
            var signValue = match.Groups["sign"].Value;
            var offsetValue = match.Groups["offset"].Value;
            if (string.IsNullOrWhiteSpace(signValue) &&
                string.IsNullOrWhiteSpace(offsetValue))
            {
                return 0;
            }

            var offset = int.Parse(offsetValue);

            return signValue == "-" ? -offset : offset;
        }

        private class IssueLocation : IIssueLocation
        {
            public bool IsPrimary { get; set; }

            public bool IsAdditional => !IsPrimary;

            public int LineNumber { get; set; }

            public string Message { get; set; }

            public string FlowId { get; set; }
        }
    }
}
