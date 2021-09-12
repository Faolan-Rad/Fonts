// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace SixLabors.Fonts.Tables.AdvancedTypographic.Gsub
{
    /// <summary>
    /// An Alternate Substitution (AlternateSubst) subtable identifies any number of aesthetic alternatives
    /// from which a user can choose a glyph variant to replace the input glyph.
    /// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#lookuptype-3-alternate-substitution-subtable"/>
    /// </summary>
    internal sealed class LookupType8SubTable
    {
        private LookupType8SubTable()
        {
        }

        public static LookupSubTable Load(BigEndianBinaryReader reader, long offset)
        {
            reader.Seek(offset, SeekOrigin.Begin);
            ushort substFormat = reader.ReadUInt16();

            return substFormat switch
            {
                1 => LookupType8Format1SubTable.Load(reader, offset),
                _ => throw new InvalidFontFileException($"Invalid value for 'substFormat' {substFormat}. Should be '1'."),
            };
        }
    }

    internal sealed class LookupType8Format1SubTable : LookupSubTable
    {
        private readonly ushort[] substituteGlyphIds;
        private readonly CoverageTable coverageTable;
        private readonly CoverageTable[] backtrackCoverageTables;
        private readonly CoverageTable[] lookaheadCoverageTables;

        private LookupType8Format1SubTable(
            ushort[] substituteGlyphIds,
            CoverageTable coverageTable,
            CoverageTable[] backtrackCoverageTables,
            CoverageTable[] lookaheadCoverageTables)
        {
            this.substituteGlyphIds = substituteGlyphIds;
            this.coverageTable = coverageTable;
            this.backtrackCoverageTables = backtrackCoverageTables;
            this.lookaheadCoverageTables = lookaheadCoverageTables;
        }

        public static LookupType8Format1SubTable Load(BigEndianBinaryReader reader, long offset)
        {
            // ReverseChainSingleSubstFormat1
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | Type     | Name                                          | Description                                  |
            // +==========+===============================================+==============================================+
            // | uint16   | substFormat                                   | Format identifier: format = 1                |
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | Offset16 | coverageOffset                                | Offset to Coverage table, from beginning     |
            // |          |                                               | of substitution subtable.                    |
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | uint16   | backtrackGlyphCount                           | Number of glyphs in the backtrack sequence.  |
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | Offset16 | backtrackCoverageOffsets[backtrackGlyphCount] | Array of offsets to coverage tables in       |
            // |          |                                               | backtrack sequence, in glyph sequence        |
            // |          |                                               | order.                                       |
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | uint16   | lookaheadGlyphCount                           | Number of glyphs in lookahead sequence.      |
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | Offset16 | lookaheadCoverageOffsets[lookaheadGlyphCount] | Array of offsets to coverage tables in       |
            // |          |                                               | lookahead sequence, in glyph sequence order. |
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | uint16   | glyphCount                                    | Number of glyph IDs in the                   |
            // |          |                                               | substituteGlyphIDs array.                    |
            // +----------+-----------------------------------------------+----------------------------------------------+
            // | uint16   | substituteGlyphIDs[glyphCount]                | Array of substitute glyph IDs — ordered      |
            // |          |                                               | by Coverage index.                           |
            // +----------+-----------------------------------------------+----------------------------------------------+
            ushort coverageOffset = reader.ReadOffset16();
            ushort backtrackGlyphCount = reader.ReadUInt16();
            ushort[] backtrackCoverageOffsets = reader.ReadUInt16Array(backtrackGlyphCount);

            ushort lookaheadGlyphCount = reader.ReadUInt16();
            ushort[] lookaheadCoverageOffsets = reader.ReadUInt16Array(lookaheadGlyphCount);

            ushort glyphCount = reader.ReadUInt16();
            ushort[] substituteGlyphIds = reader.ReadUInt16Array(glyphCount);

            var coverageTable = CoverageTable.Load(reader, offset + coverageOffset);
            CoverageTable[] backtrackCoverageTables = CoverageTable.LoadArray(reader, offset, backtrackCoverageOffsets);
            CoverageTable[] lookaheadCoverageTables = CoverageTable.LoadArray(reader, offset, lookaheadCoverageOffsets);

            return new LookupType8Format1SubTable(substituteGlyphIds, coverageTable, backtrackCoverageTables, lookaheadCoverageTables);
        }

        public override bool TrySubstition(GSubTable table, GlyphSubstitutionCollection collection, ushort index, int count)
        {
            int glyphId = collection[index][0];
            if (glyphId < 0)
            {
                return false;
            }

            int offset = this.coverageTable.CoverageIndexOf((ushort)glyphId);

            if (offset > -1)
            {
                // TODO: Implement
                // https://docs.microsoft.com/en-us/typography/opentype/spec/gsub#81-reverse-chaining-contextual-single-substitution-format-1-coverage-based-glyph-contexts
                return false;
            }

            return false;
        }

        public readonly struct AlternateSetTable
        {
            public AlternateSetTable(int[] alternateGlyphs)
                => this.AlternateGlyphs = alternateGlyphs;

            public readonly int[] AlternateGlyphs { get; }
        }
    }
}