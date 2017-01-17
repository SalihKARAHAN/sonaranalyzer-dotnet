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

using System.Linq;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SonarAnalyzer.UnitTest.TestFramework.IssueLocationCollectorTests
{
    [TestClass]
    public class IssueLocationCollector_GetIssueLocation
    {
        private TextLine GetLine(int lineNumber, string code)
        {
            var sourceText = SourceText.From(code);
            return sourceText.Lines[lineNumber];
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Two_Flows()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant [flow1,flow2]
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(3, result[0].LineNumber);
            Assert.IsNull(result[0].Message);
            Assert.AreEqual("flow1", result[0].FlowId);

            Assert.IsTrue(result[1].IsPrimary);
            Assert.IsFalse(result[1].IsAdditional);
            Assert.AreEqual(3, result[1].LineNumber);
            Assert.IsNull(result[1].Message);
            Assert.AreEqual("flow2", result[1].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Offset_Message_And_Flows()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant@-1 [flow1,flow2] {{Some message}}
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(2, result[0].LineNumber);
            Assert.AreEqual("Some message", result[0].Message);
            Assert.AreEqual("flow1", result[0].FlowId);

            Assert.IsTrue(result[1].IsPrimary);
            Assert.IsFalse(result[1].IsAdditional);
            Assert.AreEqual(2, result[1].LineNumber);
            Assert.AreEqual("Some message", result[1].Message);
            Assert.AreEqual("flow2", result[1].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Reversed_Message_And_Flows()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant {{Some message}} [flow1,flow2]
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(1, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(3, result[0].LineNumber);
            Assert.AreEqual("Some message", result[0].Message);
            Assert.IsNull(result[0].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Offset()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant@-1
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(1, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(2, result[0].LineNumber);
            Assert.IsNull(result[0].Message);
            Assert.IsNull(result[0].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Message_And_Flows()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant [flow1,flow2] {{Some message}}
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(3, result[0].LineNumber);
            Assert.AreEqual("Some message", result[0].Message);
            Assert.AreEqual("flow1", result[0].FlowId);

            Assert.IsTrue(result[1].IsPrimary);
            Assert.IsFalse(result[1].IsAdditional);
            Assert.AreEqual(3, result[1].LineNumber);
            Assert.AreEqual("Some message", result[1].Message);
            Assert.AreEqual("flow2", result[1].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Message()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant {{Some message}}
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(1, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(3, result[0].LineNumber);
            Assert.AreEqual("Some message", result[0].Message);
            Assert.IsNull(result[0].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Invalid_Offset()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant@=1
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(1, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(3, result[0].LineNumber);
            Assert.IsNull(result[0].Message);
            Assert.IsNull(result[0].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant_With_Flow()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant [flow1,flow2]
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(3, result[0].LineNumber);
            Assert.IsNull(result[0].Message);
            Assert.AreEqual("flow1", result[0].FlowId);

            Assert.IsTrue(result[1].IsPrimary);
            Assert.IsFalse(result[1].IsAdditional);
            Assert.AreEqual(3, result[1].LineNumber);
            Assert.IsNull(result[1].Message);
            Assert.AreEqual("flow2", result[1].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Noncompliant()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Noncompliant
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(1, result.Count);

            Assert.IsTrue(result[0].IsPrimary);
            Assert.IsFalse(result[0].IsAdditional);
            Assert.AreEqual(3, result[0].LineNumber);
            Assert.IsNull(result[0].Message);
            Assert.IsNull(result[0].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_Flow_With_Offset_Message_And_Flows()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a); //Flow@-1 [flow1,flow2] {{Some message}}
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(2, result.Count);

            Assert.IsFalse(result[0].IsPrimary);
            Assert.IsTrue(result[0].IsAdditional);
            Assert.AreEqual(2, result[0].LineNumber);
            Assert.AreEqual("Some message", result[0].Message);
            Assert.AreEqual("flow1", result[0].FlowId);

            Assert.IsFalse(result[1].IsPrimary);
            Assert.IsTrue(result[1].IsAdditional);
            Assert.AreEqual(2, result[1].LineNumber);
            Assert.AreEqual("Some message", result[1].Message);
            Assert.AreEqual("flow2", result[1].FlowId);
        }

        [TestMethod]
        public void GetIssueLocation_NoComment()
        {
            var line = GetLine(2, @"if (a > b)
{
    Console.WriteLine(a);
}");
            var result = new IssueLocationCollector().GetIssueLocations(line).ToList();

            Assert.AreEqual(0, result.Count);
        }
    }
}
