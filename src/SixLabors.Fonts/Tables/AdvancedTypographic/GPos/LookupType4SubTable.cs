// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.IO;

namespace SixLabors.Fonts.Tables.AdvancedTypographic.GPos
{
    /// <summary>
    /// Mark-to-Base Attachment Positioning Subtable. The MarkToBase attachment (MarkBasePos) subtable is used to position combining mark glyphs with respect to base glyphs.
    /// For example, the Arabic, Hebrew, and Thai scripts combine vowels, diacritical marks, and tone marks with base glyphs.
    /// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-4-mark-to-base-attachment-positioning-subtable"/>
    /// </summary>
    internal static class LookupType4SubTable
    {
        public static LookupSubTable Load(BigEndianBinaryReader reader, long offset)
        {
            reader.Seek(offset, SeekOrigin.Begin);
            ushort posFormat = reader.ReadUInt16();

            return posFormat switch
            {
                1 => LookupType4Format1SubTable.Load(reader, offset),
                _ => throw new InvalidFontFileException(
                    $"Invalid value for 'posFormat' {posFormat}. Should be '1'.")
            };
        }

        internal sealed class LookupType4Format1SubTable : LookupSubTable
        {
            private readonly CoverageTable markCoverage;
            private readonly CoverageTable baseCoverage;
            private readonly MarkArrayTable markArrayTable;
            private readonly BaseArrayTable baseArrayTable;

            public LookupType4Format1SubTable(CoverageTable markCoverage, CoverageTable baseCoverage, MarkArrayTable markArrayTable, BaseArrayTable baseArrayTable)
            {
                this.markCoverage = markCoverage;
                this.baseCoverage = baseCoverage;
                this.markArrayTable = markArrayTable;
                this.baseArrayTable = baseArrayTable;
            }

            public static LookupType4Format1SubTable Load(BigEndianBinaryReader reader, long offset)
            {
                // MarkBasePosFormat1 Subtable.
                // +--------------------+---------------------------------+------------------------------------------------------+
                // | Type               |  Name                           | Description                                          |
                // +====================+=================================+======================================================+
                // | uint16             | posFormat                       | Format identifier: format = 1                        |
                // +--------------------+---------------------------------+------------------------------------------------------+
                // | Offset16           | markCoverageOffset              | Offset to markCoverage table,                        |
                // |                    |                                 | from beginning of MarkBasePos subtable.              |
                // +--------------------+---------------------------------+------------------------------------------------------+
                // | Offset16           | baseCoverageOffset              | Offset to baseCoverage table,                        |
                // |                    |                                 | from beginning of MarkBasePos subtable.              |
                // +--------------------+---------------------------------+------------------------------------------------------+
                // | uint16             | markClassCount                  | Number of classes defined for marks.                 |
                // +--------------------+---------------------------------+------------------------------------------------------+
                // | Offset16           | markArrayOffset                 | Offset to MarkArray table,                           |
                // |                    |                                 | from beginning of MarkBasePos subtable.              |
                // +--------------------+---------------------------------+------------------------------------------------------+
                // | Offset16           | baseArrayOffset                 | Offset to BaseArray table,                           |
                // |                    |                                 | from beginning of MarkBasePos subtable.              |
                // +--------------------+---------------------------------+------------------------------------------------------+
                ushort markCoverageOffset = reader.ReadOffset16();
                ushort baseCoverageOffset = reader.ReadOffset16();
                ushort markClassCount = reader.ReadUInt16();
                ushort markArrayOffset = reader.ReadOffset16();
                ushort baseArrayOffset = reader.ReadOffset16();

                var markCoverage = CoverageTable.Load(reader, offset + markCoverageOffset);
                var baseCoverage = CoverageTable.Load(reader, offset + baseCoverageOffset);
                var markArrayTable = new MarkArrayTable(reader, offset + markArrayOffset);
                var baseArrayTable = new BaseArrayTable(reader, offset + baseArrayOffset, markClassCount);

                return new LookupType4Format1SubTable(markCoverage, baseCoverage, markArrayTable, baseArrayTable);
            }

            public override bool TryUpdatePosition(IFontMetrics fontMetrics, GPosTable table, GlyphPositioningCollection collection, ushort index, int count)
                => throw new System.NotImplementedException();
        }
    }
}
