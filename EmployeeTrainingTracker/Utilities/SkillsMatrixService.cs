using Npgsql;
using System;
using System.Collections.Generic;

namespace EmployeeTrainingTracker.Utilities
{
    /// <summary>
    /// READ-ONLY access to the Skills Matrix: a representation of approved data imported from a
    /// verified copy of the audited Skills Matrix workbook (schema: Database/Migrations/004).
    /// This class never writes to the database or to any workbook. Data is only loaded by the
    /// separately approved import tool.
    ///
    /// Access: every public method first re-checks, against the database, that the caller is an
    /// active (non-archived) Admin. This is an APPLICATION-LEVEL check, the same pattern as
    /// ManagerAccessService - it is not database-enforced security (see docs/SkillsMatrix.md).
    ///
    /// Every read is anchored to a specific ImportID, so the view stays consistent even if the
    /// current snapshot is replaced while it is open (snapshots themselves are immutable).
    /// </summary>
    public static class SkillsMatrixService
    {
        public enum SkillsMatrixState
        {
            NotInstalled,       // migration 004 has not been run
            NoApprovedImport,   // tables exist, but no approved import is current
            Ready
        }

        public sealed record SkillMatrixImportInfo(long ImportId, string SourceFileName, string SourceSha256,
            string? SourceVersion, string ApprovedBy, DateTime ImportedAt, string? Notes);

        public sealed record SkillAreaInfo(long AreaId, string Name, string SourceSheet,
            string? SkillHeaderRef = null, string? InabHeaderRef = null);

        public sealed record SkillLegendEntry(int KeyNumber, int Level, string Label, string? Description, string? SourceRef = null);

        public sealed record SkillLegend(string ScaleName, bool NeedsConfirmation, string? LegendNote,
            string SourceRef, IReadOnlyList<SkillLegendEntry> Entries);

        public sealed record SkillCategoryInfo(long CategoryId, string Name, string SourceRef);

        public sealed record SkillRowInfo(long SkillId, long CategoryId, string Name, bool? InabAccredited,
            string? InabRaw, bool HiddenInSource, string SourceRef, string? InabSourceRef = null);

        public sealed record SkillMemberInfo(long MemberId, long EmployeeId, string EmployeeName, bool EmployeeArchived,
            string SourceHeaderText, string? SubGroupLabel, string SourceColumn,
            string? HeaderSourceRef = null, string? SubGroupSourceRef = null);

        /// <summary>Status is 'Level', 'Unassessed' (blank in source) or 'Flagged'. A missing cell = outside the source range.</summary>
        public sealed record SkillCellInfo(string Status, int? Level, string? RawValue, string? FlagReason, string SourceCell);

        public sealed record SkillExcludedColumn(string SourceColumn, string SourceHeaderText, string Reason);

        public sealed record SkillMatrixData(
            SkillAreaInfo Area,
            SkillLegend Legend,
            IReadOnlyList<SkillCategoryInfo> Categories,
            IReadOnlyList<SkillRowInfo> Skills,
            IReadOnlyList<SkillMemberInfo> Members,
            IReadOnlyDictionary<(long SkillId, long MemberId), SkillCellInfo> Cells,
            IReadOnlyList<SkillExcludedColumn> ExcludedColumns,
            // The workbook's displayed formatting of the imported cells (SkillMatrixCellFormats), keyed by "Sheet!A1"
            IReadOnlyDictionary<string, SkillsMatrixWorkbookColours.CellDisplay>? Formats = null);

        // ------------------------------------------------------------------------------------------

        /// <summary>
        /// Application-level authorisation: throws unless the employee has an Admin login and is not archived.
        /// Re-read from the database on every call rather than trusting the role captured at login.
        /// </summary>
        public static void EnsureAdmin(int employeeId)
        {
            if (employeeId <= 0)
                throw new UnauthorizedAccessException("The Skills Matrix is available to Admin users only.");

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT EXISTS (
                    SELECT 1 FROM Users u
                    INNER JOIN Employees e ON e.EmployeeID = u.EmployeeID
                    WHERE u.EmployeeID = @id
                      AND u.Role = 'Admin'
                      AND NOT e.IsArchived)", conn);
            cmd.Parameters.AddWithValue("id", (long)employeeId);

            if (cmd.ExecuteScalar() is not true)
                throw new UnauthorizedAccessException("The Skills Matrix is available to Admin users only.");
        }

        /// <summary>
        /// Whether the Skills Matrix is installed and, if so, the current approved import (null when there is none).
        /// </summary>
        public static (SkillsMatrixState State, SkillMatrixImportInfo? Import) GetCurrentImport(int adminEmployeeId)
        {
            EnsureAdmin(adminEmployeeId);

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using (var check = new NpgsqlCommand("SELECT to_regclass('public.skillmatriximports') IS NOT NULL", conn))
            {
                if (check.ExecuteScalar() is not true)
                    return (SkillsMatrixState.NotInstalled, null);
            }

            using var cmd = new NpgsqlCommand(@"
                SELECT ImportID, SourceFileName, SourceSha256, SourceVersion, ApprovedBy, ImportedAt, Notes
                FROM SkillMatrixImports
                WHERE IsCurrent", conn);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return (SkillsMatrixState.NoApprovedImport, null);

            var import = new SkillMatrixImportInfo(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2).Trim(),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetDateTime(5),
                reader.IsDBNull(6) ? null : reader.GetString(6));

            return (SkillsMatrixState.Ready, import);
        }

        /// <summary>Sheets (areas) in the given import, in source order.</summary>
        public static List<SkillAreaInfo> GetAreas(int adminEmployeeId, long importId)
        {
            EnsureAdmin(adminEmployeeId);

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            using var cmd = new NpgsqlCommand(@"
                SELECT AreaID, Name, SourceSheet, SkillHeaderRef, InabHeaderRef
                FROM SkillMatrixAreas
                WHERE ImportID = @importId
                ORDER BY DisplayOrder, AreaID", conn);
            cmd.Parameters.AddWithValue("importId", importId);

            var areas = new List<SkillAreaInfo>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                areas.Add(new SkillAreaInfo(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4)));
            return areas;
        }

        /// <summary>
        /// Everything needed to display one sheet of the given import: legend, categories, skills,
        /// confirmed members, cells and the source columns that were not imported.
        /// </summary>
        public static SkillMatrixData GetMatrix(int adminEmployeeId, long importId, long areaId)
        {
            EnsureAdmin(adminEmployeeId);

            using var conn = DatabaseHelper.GetConnection();
            conn.Open();

            // Area + scale (both must belong to the requested import)
            SkillAreaInfo area;
            long scaleId;
            string scaleName, scaleSourceRef;
            bool needsConfirmation;
            string? legendNote;
            using (var cmd = new NpgsqlCommand(@"
                SELECT a.AreaID, a.Name, a.SourceSheet, s.ScaleID, s.Name, s.LegendStatus, s.LegendNote, s.SourceRef,
                       a.SkillHeaderRef, a.InabHeaderRef
                FROM SkillMatrixAreas a
                INNER JOIN SkillMatrixScales s ON s.ScaleID = a.ScaleID AND s.ImportID = a.ImportID
                WHERE a.AreaID = @areaId AND a.ImportID = @importId", conn))
            {
                cmd.Parameters.AddWithValue("areaId", areaId);
                cmd.Parameters.AddWithValue("importId", importId);
                using var reader = cmd.ExecuteReader();
                if (!reader.Read())
                    throw new InvalidOperationException("That sheet is not part of the current Skills Matrix import.");

                area = new SkillAreaInfo(reader.GetInt64(0), reader.GetString(1), reader.GetString(2),
                    reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9));
                scaleId = reader.GetInt64(3);
                scaleName = reader.GetString(4);
                needsConfirmation = reader.GetString(5) == "NeedsConfirmation";
                legendNote = reader.IsDBNull(6) ? null : reader.GetString(6);
                scaleSourceRef = reader.GetString(7);
            }

            var entries = new List<SkillLegendEntry>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT KeyNumber, Level, Label, Description, SourceRef
                FROM SkillMatrixScaleLevels
                WHERE ScaleID = @scaleId
                ORDER BY KeyNumber, Level", conn))
            {
                cmd.Parameters.AddWithValue("scaleId", scaleId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    entries.Add(new SkillLegendEntry(reader.GetInt16(0), reader.GetInt16(1), reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4)));
            }

            var categories = new List<SkillCategoryInfo>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT CategoryID, Name, SourceRef
                FROM SkillMatrixCategories
                WHERE AreaID = @areaId
                ORDER BY DisplayOrder, CategoryID", conn))
            {
                cmd.Parameters.AddWithValue("areaId", areaId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    categories.Add(new SkillCategoryInfo(reader.GetInt64(0), reader.GetString(1), reader.GetString(2)));
            }

            var skills = new List<SkillRowInfo>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT SkillID, CategoryID, Name, InabAccredited, InabRaw, HiddenInSource, SourceRef, InabSourceRef
                FROM SkillMatrixSkills
                WHERE AreaID = @areaId
                ORDER BY DisplayOrder, SourceRow", conn))
            {
                cmd.Parameters.AddWithValue("areaId", areaId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    skills.Add(new SkillRowInfo(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetBoolean(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.GetBoolean(5), reader.GetString(6),
                        reader.IsDBNull(7) ? null : reader.GetString(7)));
            }

            var members = new List<SkillMemberInfo>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT c.ColumnID, c.EmployeeID, COALESCE(e.FullName, '(employee record not found)'),
                       COALESCE(e.IsArchived, FALSE), c.SourceHeaderText, c.SubGroupLabel, c.SourceColumn,
                       c.HeaderSourceRef, c.SubGroupSourceRef
                FROM SkillMatrixColumns c
                LEFT JOIN Employees e ON e.EmployeeID = c.EmployeeID
                WHERE c.AreaID = @areaId AND c.Disposition = 'Member'
                ORDER BY c.DisplayOrder, c.ColumnID", conn))
            {
                cmd.Parameters.AddWithValue("areaId", areaId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    members.Add(new SkillMemberInfo(reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                        reader.GetBoolean(3), reader.GetString(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6),
                        reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8)));
            }

            var cells = new Dictionary<(long, long), SkillCellInfo>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT SkillID, ColumnID, Status, Level, RawValue, FlagReason, SourceCell
                FROM SkillMatrixCells
                WHERE AreaID = @areaId", conn))
            {
                cmd.Parameters.AddWithValue("areaId", areaId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    cells[(reader.GetInt64(0), reader.GetInt64(1))] = new SkillCellInfo(
                        reader.GetString(2),
                        reader.IsDBNull(3) ? null : reader.GetInt16(3),
                        reader.IsDBNull(4) ? null : reader.GetString(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5),
                        reader.GetString(6));
            }

            var excluded = new List<SkillExcludedColumn>();
            using (var cmd = new NpgsqlCommand(@"
                SELECT SourceColumn, SourceHeaderText, ExcludeReason
                FROM SkillMatrixColumns
                WHERE AreaID = @areaId AND Disposition = 'Excluded'
                ORDER BY LENGTH(SourceColumn), SourceColumn", conn))
            {
                cmd.Parameters.AddWithValue("areaId", areaId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    excluded.Add(new SkillExcludedColumn(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }

            // The workbook's displayed formatting, as captured at import time
            var formats = new Dictionary<string, SkillsMatrixWorkbookColours.CellDisplay>(StringComparer.Ordinal);
            using (var cmd = new NpgsqlCommand(@"
                SELECT SourceRef, FillSource, FillRgb, Pattern, PatternColorRgb, PatternBackRgb,
                       ConditionalRule, FontSource, FontRgb, FontRule, Bold
                FROM SkillMatrixCellFormats
                WHERE AreaID = @areaId", conn))
            {
                cmd.Parameters.AddWithValue("areaId", areaId);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string? S(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
                    formats[reader.GetString(0)] = new SkillsMatrixWorkbookColours.CellDisplay(
                        FromHex(S(2)), FromHex(S(8)) ?? System.Drawing.Color.Black, reader.GetBoolean(10),
                        S(3), FromHex(S(4)), FromHex(S(5)), reader.GetString(1), S(6), reader.GetString(7), S(9));
                }
            }

            var legend = new SkillLegend(scaleName, needsConfirmation, legendNote, scaleSourceRef, entries);
            return new SkillMatrixData(area, legend, categories, skills, members, cells, excluded, formats);
        }

        private static System.Drawing.Color? FromHex(string? rgb) =>
            rgb is { Length: 6 }
                ? System.Drawing.Color.FromArgb(Convert.ToInt32(rgb[0..2], 16), Convert.ToInt32(rgb[2..4], 16), Convert.ToInt32(rgb[4..6], 16))
                : null;
    }
}
