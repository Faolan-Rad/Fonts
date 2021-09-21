// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SixLabors.Fonts.Tables.AdvancedTypographic.GPos
{
    /// <summary>
    /// A pair adjustment positioning subtable (PairPos) is used to adjust the placement or advances of two glyphs in relation to one another —
    /// for instance, to specify kerning data for pairs of glyphs. Compared to a typical kerning table, however,
    /// a PairPos subtable offers more flexibility and precise control over glyph positioning.
    /// The PairPos subtable can adjust each glyph in a pair independently in both the X and Y directions,
    /// and it can explicitly describe the particular type of adjustment applied to each glyph.
    /// PairPos subtables can be either of two formats: one that identifies glyphs individually by index(Format 1), and one that identifies glyphs by class (Format 2).
    /// <see href="https://docs.microsoft.com/en-us/typography/opentype/spec/gpos#lookup-type-2-pair-adjustment-positioning-subtable"/>
    /// </summary>
    internal static class LookupType2SubTable
    {
        public static LookupSubTable Load(BigEndianBinaryReader reader, long offset)
        {
            reader.Seek(offset, SeekOrigin.Begin);
            ushort posFormat = reader.ReadUInt16();

            return posFormat switch
            {
                1 => LookupType2Format1SubTable.Load(reader, offset),
                2 => LookupType2Format2SubTable.Load(reader, offset),
                _ => throw new InvalidFontFileException(
                    $"Invalid value for 'posFormat' {posFormat}. Should be '1' or '2'.")
            };
        }

        internal sealed class LookupType2Format1SubTable : LookupSubTable
        {
            private readonly CoverageTable coverageTable;
            private readonly PairSetTable[] pairSets;

            public LookupType2Format1SubTable(CoverageTable coverageTable, PairSetTable[] pairSets)
            {
                this.coverageTable = coverageTable;
                this.pairSets = pairSets;
            }

            public static LookupType2Format1SubTable Load(BigEndianBinaryReader reader, long offset)
            {
                // Pair Adjustment Positioning Subtable format 1.
                // +-------------+------------------------------+------------------------------------------------+
                // | Type        |  Name                        | Description                                    |
                // +=============+==============================+================================================+
                // | uint16      | posFormat                    | Format identifier: format = 1                  |
                // +-------------+------------------------------+------------------------------------------------+
                // | Offset16    | coverageOffset               | Offset to Coverage table, from beginning of    |
                // |             |                              | PairPos subtable.                              |
                // +-------------+------------------------------+------------------------------------------------+
                // | uint16      | valueFormat1                 | Defines the types of data in valueRecord1 —    |
                // |             |                              | for the first glyph in the pair (may be zero). |
                // +-------------+------------------------------+------------------------------------------------+
                // | uint16      | valueFormat2                 | Defines the types of data in valueRecord2 —    |
                // |             |                              | for the second glyph in the pair (may be zero).|
                // +-------------+------------------------------+------------------------------------------------+
                // | uint16      | pairSetCount                 | Number of PairSet tables                       |
                // +-------------+------------------------------+------------------------------------------------+
                // | Offset16    | pairSetOffsets[pairSetCount] | Array of offsets to PairSet tables.            |
                // |             |                              | Offsets are from beginning of PairPos subtable,|
                // |             |                              | ordered by Coverage Index.                     |
                // +-------------+------------------------------+------------------------------------------------+
                ushort coverageOffset = reader.ReadOffset16();
                ValueFormat valueFormat1 = reader.ReadUInt16<ValueFormat>();
                ValueFormat valueFormat2 = reader.ReadUInt16<ValueFormat>();
                ushort pairSetCount = reader.ReadUInt16();
                ushort[] pairSetOffsets = reader.ReadUInt16Array(pairSetCount);

                var pairSets = new PairSetTable[pairSetCount];
                for (int i = 0; i < pairSetCount; i++)
                {
                    reader.Seek(offset + pairSetOffsets[i], SeekOrigin.Begin);
                    pairSets[i] = PairSetTable.Load(reader, offset + pairSetOffsets[i], valueFormat1, valueFormat2);
                }

                var coverageTable = CoverageTable.Load(reader, offset + coverageOffset);

                return new LookupType2Format1SubTable(coverageTable, pairSets);
            }

            public override bool TryUpdatePosition(
                IFontMetrics fontMetrics,
                GPosTable table,
                GlyphPositioningCollection collection,
                ushort index,
                int count)
            {
                for (ushort i = 0; i < count - 1; i++)
                {
                    int glyphId = collection.GetGlyphIds(i + index)[0];
                    if (glyphId < 0)
                    {
                        return false;
                    }

                    int coverage = this.coverageTable.CoverageIndexOf((ushort)glyphId);
                    if (coverage > -1)
                    {
                        PairSetTable pairSet = this.pairSets[coverage];
                        int glyphId2 = collection.GetGlyphIds(i + 1 + index)[0];
                        if (glyphId2 < 0)
                        {
                            return false;
                        }

                        if (pairSet.TryGetPairValueRecord((ushort)glyphId2, out PairValueRecord pairValueRecord))
                        {
                            ValueRecord record1 = pairValueRecord.ValueRecord1;
                            collection.Offset(fontMetrics, i, (ushort)glyphId, record1.XPlacement, record1.YPlacement);
                            collection.Advance(fontMetrics, i, (ushort)glyphId, record1.XAdvance, record1.YAdvance);

                            ValueRecord record2 = pairValueRecord.ValueRecord2;
                            collection.Offset(fontMetrics, (ushort)(i + 1), (ushort)glyphId2, record2.XPlacement, record2.YPlacement);
                            collection.Advance(fontMetrics, (ushort)(i + 1), (ushort)glyphId2, record2.XAdvance, record2.YAdvance);

                            return true;
                        }
                    }
                }

                return false;
            }

            internal sealed class PairSetTable
            {
                private readonly PairValueRecord[] pairValueRecords;

                private PairSetTable(PairValueRecord[] pairValueRecords)
                    => this.pairValueRecords = pairValueRecords;

                public static PairSetTable Load(BigEndianBinaryReader reader, long offset, ValueFormat valueFormat1, ValueFormat valueFormat2)
                {
                    // +-----------------+----------------------------------+---------------------------------------+
                    // | Type            | Name                             | Description                           |
                    // +=================+==================================+=======================================+
                    // | uint16          | pairValueCount                   | Number of PairValueRecords            |
                    // +-----------------+----------------------------------+---------------------------------------+
                    // | PairValueRecord | pairValueRecords[pairValueCount] | Array of PairValueRecords, ordered by |
                    // |                 |                                  | glyph ID of the second glyph.         |
                    // +-----------------+----------------------------------+---------------------------------------+
                    reader.Seek(offset, SeekOrigin.Begin);
                    ushort pairValueCount = reader.ReadUInt16();
                    var pairValueRecords = new PairValueRecord[pairValueCount];
                    for (int i = 0; i < pairValueRecords.Length; i++)
                    {
                        pairValueRecords[i] = new PairValueRecord(reader, valueFormat1, valueFormat2);
                    }

                    return new PairSetTable(pairValueRecords);
                }

                public bool TryGetPairValueRecord(ushort glyphId, [NotNullWhen(true)] out PairValueRecord pairValueRecord)
                {
                    foreach (PairValueRecord pair in this.pairValueRecords)
                    {
                        if (pair.SecondGlyph == glyphId)
                        {
                            pairValueRecord = pair;
                            return true;
                        }
                    }

                    pairValueRecord = default;
                    return false;
                }
            }
        }

        internal sealed class LookupType2Format2SubTable : LookupSubTable
        {
            private readonly CoverageTable coverageTable;
            private readonly Class1Record[] class1Records;
            private readonly ClassDefinitionTable classDefinitionTable1;
            private readonly ClassDefinitionTable classDefinitionTable2;

            public LookupType2Format2SubTable(
                CoverageTable coverageTable,
                Class1Record[] class1Records,
                ClassDefinitionTable classDefinitionTable1,
                ClassDefinitionTable classDefinitionTable2)
            {
                this.coverageTable = coverageTable;
                this.class1Records = class1Records;
                this.classDefinitionTable1 = classDefinitionTable1;
                this.classDefinitionTable2 = classDefinitionTable2;
            }

            public static LookupType2Format2SubTable Load(BigEndianBinaryReader reader, long offset)
            {
                // Pair Adjustment Positioning Subtable format 2.
                // +-------------+------------------------------+------------------------------------------------+
                // | Type        |  Name                        | Description                                    |
                // +=============+==============================+================================================+
                // | uint16      | posFormat                    | Format identifier: format = 2                  |
                // +-------------+------------------------------+------------------------------------------------+
                // | Offset16    | coverageOffset               | Offset to Coverage table, from beginning of    |
                // |             |                              | PairPos subtable.                              |
                // +-------------+------------------------------+------------------------------------------------+
                // | uint16      | valueFormat1                 | Defines the types of data in valueRecord1 —    |
                // |             |                              | for the first glyph in the pair (may be zero). |
                // +-------------+------------------------------+------------------------------------------------+
                // | uint16      | valueFormat2                 | Defines the types of data in valueRecord2 —    |
                // |             |                              | for the second glyph in the pair (may be zero).|
                // +-------------+------------------------------+------------------------------------------------+
                // | Offset16    | classDef1Offset              | Offset to ClassDef table, from beginning of    |
                // |             |                              | PairPos subtable —                             |
                // |             |                              | for the first glyph of the pair.               |
                // +-------------+------------------------------+------------------------------------------------+
                // | Offset16    | classDef2Offset              | Offset to ClassDef table, from beginning of    |
                // |             |                              | PairPos subtable —                             |
                // |             |                              | for the second glyph of the pair. —            |
                // +-------------+------------------------------+------------------------------------------------+
                // | uint16      | class1Count                  | Number of classes in classDef1 table —         |
                // |             |                              | includes Class 0.                              |
                // +-------------+------------------------------+------------------------------------------------+
                // | uint16      | class2Count                  | Number of classes in classDef2 table —         |
                // |             |                              | includes Class 0.                              |
                // +-------------+------------------------------+------------------------------------------------+
                // | Class1Record| class1Records[class1Count]   | Array of Class1 records,                       |
                // |             |                              | ordered by classes in classDef1.               |
                // +-------------+------------------------------+------------------------------------------------+
                ushort coverageOffset = reader.ReadOffset16();
                ValueFormat valueFormat1 = reader.ReadUInt16<ValueFormat>();
                ValueFormat valueFormat2 = reader.ReadUInt16<ValueFormat>();
                ushort classDef1Offset = reader.ReadOffset16();
                ushort classDef2Offset = reader.ReadOffset16();
                ushort class1Count = reader.ReadUInt16();
                ushort class2Count = reader.ReadUInt16();

                var class1Records = new Class1Record[class1Count];
                for (int i = 0; i < class1Records.Length; i++)
                {
                    class1Records[i] = Class1Record.Load(reader, class2Count, valueFormat1, valueFormat2);
                }

                var coverageTable = CoverageTable.Load(reader, offset + coverageOffset);
                var classDefTable1 = ClassDefinitionTable.Load(reader, offset + classDef1Offset);
                var classDefTable2 = ClassDefinitionTable.Load(reader, offset + classDef2Offset);

                return new LookupType2Format2SubTable(coverageTable, class1Records, classDefTable1, classDefTable2);
            }

            public override bool TryUpdatePosition(
                IFontMetrics fontMetrics,
                GPosTable table,
                GlyphPositioningCollection collection,
                ushort index,
                int count)
            {
                for (ushort i = 0; i < count - 1; i++)
                {
                    int glyphId = collection.GetGlyphIds(i + index)[0];
                    if (glyphId < 0)
                    {
                        return false;
                    }

                    int coverage = this.coverageTable.CoverageIndexOf((ushort)glyphId);
                    if (coverage > -1)
                    {
                        int classDef1 = this.classDefinitionTable1.ClassIndexOf((ushort)glyphId);
                        int glyphId2 = collection.GetGlyphIds(i + 1 + index)[0];
                        if (glyphId2 < 0)
                        {
                            return false;
                        }

                        int classDef2 = this.classDefinitionTable2.ClassIndexOf((ushort)glyphId2);

                        Class1Record class1Record = this.class1Records[classDef1];
                        Class2Record class2Record = class1Record.Class2Records[classDef2];

                        ValueRecord record1 = class2Record.ValueRecord1;
                        collection.Offset(fontMetrics, i, (ushort)glyphId, record1.XPlacement, record1.YPlacement);
                        collection.Advance(fontMetrics, i, (ushort)glyphId, record1.XAdvance, record1.YAdvance);

                        ValueRecord record2 = class2Record.ValueRecord2;
                        collection.Offset(fontMetrics, (ushort)(i + 1), (ushort)glyphId2, record2.XPlacement, record2.YPlacement);
                        collection.Advance(fontMetrics, (ushort)(i + 1), (ushort)glyphId2, record2.XAdvance, record2.YAdvance);

                        return true;
                    }
                }

                return false;
            }

            internal sealed class Class1Record
            {
                private Class1Record(Class2Record[] class2Records) => this.Class2Records = class2Records;

                public Class2Record[] Class2Records { get; }

                public static Class1Record Load(BigEndianBinaryReader reader, int class2Count, ValueFormat valueFormat1, ValueFormat valueFormat2)
                {
                    // +--------------+----------------------------+---------------------------------------------+
                    // | Type         | Name                       | Description                                 |
                    // +==============+============================+=============================================+
                    // | Class2Record | class2Records[class2Count] | Array of Class2 records, ordered by classes |
                    // |              |                            | in classDef2.                               |
                    // +--------------+----------------------------+---------------------------------------------+
                    var class2Records = new Class2Record[class2Count];
                    for (int i = 0; i < class2Records.Length; i++)
                    {
                        class2Records[i] = new Class2Record(reader, valueFormat1, valueFormat2);
                    }

                    return new Class1Record(class2Records);
                }
            }
        }
    }
}
