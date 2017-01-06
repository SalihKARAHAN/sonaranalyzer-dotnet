﻿/*
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

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Rules.Common;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using SonarAnalyzer.Helpers;

namespace SonarAnalyzer.Rules.VisualBasic
{
    [DiagnosticAnalyzer(LanguageNames.VisualBasic)]
    [Rule(DiagnosticId)]
    public class FlagsEnumZeroMember : FlagsEnumZeroMemberBase<SyntaxKind, EnumStatementSyntax, EnumMemberDeclarationSyntax>
    {
        protected static readonly DiagnosticDescriptor rule =
            DiagnosticDescriptorBuilder.GetDescriptor(DiagnosticId, MessageFormat, RspecStrings.ResourceManager);

        protected override DiagnosticDescriptor Rule => rule;

        private static readonly ImmutableArray<SyntaxKind> kindsOfInterest = ImmutableArray.Create(SyntaxKind.EnumStatement);
        public override ImmutableArray<SyntaxKind> SyntaxKindsOfInterest => kindsOfInterest;

        protected override SyntaxToken GetIdentifier(EnumMemberDeclarationSyntax zeroMember) => zeroMember.Identifier;

        protected override IEnumerable<EnumMemberDeclarationSyntax> GetMembers(EnumStatementSyntax node)
        {
            var parent = node.Parent as EnumBlockSyntax;
            return parent?.ChildNodes().OfType<EnumMemberDeclarationSyntax>() ?? new EnumMemberDeclarationSyntax[0];
        }

        protected sealed override GeneratedCodeRecognizer GeneratedCodeRecognizer => Helpers.VisualBasic.GeneratedCodeRecognizer.Instance;
    }
}