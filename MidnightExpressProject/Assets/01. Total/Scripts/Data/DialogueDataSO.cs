using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExcelData
{
    public enum DialogueEventType
    {
        Appeared,
        Script
    }

    [Serializable]
    public sealed class PreCastingDialogueData
    {
        public int ID;
        public string Name;
        public int NextID;
        public DialogueEventType EventType = DialogueEventType.Script;
        public int Turn = 1;

        [ExcelPreserveWhitespace]
        public string Script;
    }

    [Serializable]
    public sealed class CastingDialogueData
    {
        public int ID;
        public string Name;

        [ExcelPreserveWhitespace]
        public string Script;
    }

    [ExcelAsset(
        ExcelName = "DialogueData",
        HeaderRow = 1,
        DataStartRow = 2,
        DataStartColumn = 0,
        AssetPath = "01. Total/Data",
        LogOnImport = true)]
    public sealed class DialogueDataSO : ScriptableObject, IExcelImportValidator
    {
        [ExcelSheet("Pre-CastingDialogue", HeaderRow = 0, DataStartRow = 1)]
        public List<PreCastingDialogueData> PreCastingDialogues = new();

        [ExcelSheet("CastingDialogue")]
        public List<CastingDialogueData> CastingDialogues = new();

        public void ValidateImportedData()
        {
            var errors = new List<string>();
            errors.AddRange(ValidatePreCastingDialogues());
            errors.AddRange(ValidateCastingDialogues());

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "DialogueData validation failed:" + Environment.NewLine +
                    string.Join(Environment.NewLine, errors));
            }
        }

        private IEnumerable<string> ValidatePreCastingDialogues()
        {
            const string sheetName = "Pre-CastingDialogue";
            var rowsById = new Dictionary<int, PreCastingDialogueData>();

            if (PreCastingDialogues == null)
            {
                yield return $"[{sheetName}] The dialogue list is null.";
                yield break;
            }

            for (int index = 0; index < PreCastingDialogues.Count; index++)
            {
                PreCastingDialogueData row = PreCastingDialogues[index];
                int excelRow = index + 2;

                if (row == null)
                {
                    yield return $"[{sheetName}] Row {excelRow} is null.";
                    continue;
                }

                if (row.ID <= 0)
                {
                    yield return $"[{sheetName}] Row {excelRow} has invalid ID {row.ID}. ID must be greater than zero.";
                }
                else if (!rowsById.TryAdd(row.ID, row))
                {
                    yield return $"[{sheetName}] Duplicate ID {row.ID} was found at row {excelRow}.";
                }

                if (string.IsNullOrWhiteSpace(row.Name))
                {
                    yield return $"[{sheetName}] ID {row.ID} at row {excelRow} requires a non-empty Name.";
                }

                if (!Enum.IsDefined(typeof(DialogueEventType), row.EventType))
                {
                    yield return $"[{sheetName}] ID {row.ID} has unsupported EventType '{row.EventType}'.";
                }

                if (row.EventType == DialogueEventType.Script && row.Turn < 1)
                {
                    yield return $"[{sheetName}] Script ID {row.ID} has invalid Turn {row.Turn}. Turn must be at least 1.";
                }

                if (row.EventType == DialogueEventType.Script && string.IsNullOrWhiteSpace(row.Script))
                {
                    yield return $"[{sheetName}] Script ID {row.ID} requires dialogue text in Script.";
                }

            }

            foreach (KeyValuePair<int, PreCastingDialogueData> pair in rowsById)
            {
                int nextId = pair.Value.NextID;
                if (nextId > 0 && !rowsById.ContainsKey(nextId))
                {
                    yield return $"[{sheetName}] ID {pair.Key} points to missing NextID {nextId}.";
                }
            }

            foreach (string error in ValidateNoEndlessCycles(rowsById, sheetName))
            {
                yield return error;
            }
        }

        private IEnumerable<string> ValidateCastingDialogues()
        {
            const string sheetName = "CastingDialogue";
            var registeredIds = new HashSet<int>();

            if (CastingDialogues == null)
            {
                yield return $"[{sheetName}] The dialogue list is null.";
                yield break;
            }

            for (int index = 0; index < CastingDialogues.Count; index++)
            {
                CastingDialogueData row = CastingDialogues[index];
                int excelRow = index + 3;

                if (row == null)
                {
                    yield return $"[{sheetName}] Row {excelRow} is null.";
                    continue;
                }

                if (row.ID <= 0)
                {
                    yield return $"[{sheetName}] Row {excelRow} has invalid ID {row.ID}. ID must be greater than zero.";
                }
                else if (!registeredIds.Add(row.ID))
                {
                    yield return $"[{sheetName}] Duplicate ID {row.ID} was found at row {excelRow}.";
                }

                if (string.IsNullOrWhiteSpace(row.Name))
                {
                    yield return $"[{sheetName}] ID {row.ID} at row {excelRow} requires a non-empty Name.";
                }
            }
        }

        private static IEnumerable<string> ValidateNoEndlessCycles(
            IReadOnlyDictionary<int, PreCastingDialogueData> rowsById,
            string sheetName)
        {
            var completed = new HashSet<int>();
            var reportedCycles = new HashSet<int>();

            foreach (int startId in rowsById.Keys)
            {
                if (completed.Contains(startId))
                {
                    continue;
                }

                var path = new List<int>();
                var pathIndices = new Dictionary<int, int>();
                int currentId = startId;

                while (rowsById.TryGetValue(currentId, out PreCastingDialogueData current))
                {
                    if (completed.Contains(currentId))
                    {
                        break;
                    }

                    if (pathIndices.TryGetValue(currentId, out int cycleStartIndex))
                    {
                        var cycleIds = path.GetRange(cycleStartIndex, path.Count - cycleStartIndex);
                        int cycleKey = GetSmallestId(cycleIds);

                        if (reportedCycles.Add(cycleKey))
                        {
                            cycleIds.Add(currentId);
                            yield return $"[{sheetName}] Non-terminating NextID cycle detected: {string.Join(" -> ", cycleIds)}.";
                        }

                        break;
                    }

                    pathIndices.Add(currentId, path.Count);
                    path.Add(currentId);

                    if (current.NextID <= 0)
                    {
                        break;
                    }

                    currentId = current.NextID;
                }

                foreach (int visitedId in path)
                {
                    completed.Add(visitedId);
                }
            }
        }

        private static int GetSmallestId(IReadOnlyList<int> ids)
        {
            int smallest = ids[0];
            for (int index = 1; index < ids.Count; index++)
            {
                if (ids[index] < smallest)
                {
                    smallest = ids[index];
                }
            }

            return smallest;
        }
    }
}
