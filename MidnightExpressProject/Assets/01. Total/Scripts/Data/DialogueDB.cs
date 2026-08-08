using System;
using System.Collections.Generic;
using ExcelData;
using VContainer;

public sealed class DialogueDB
{
    private readonly Dictionary<int, PreCastingDialogueData> _preCastingDialogues;
    private readonly Dictionary<int, CastingDialogueData> _castingDialogues;

    [Inject]
    public DialogueDB(DialogueDataSO dialogueData)
    {
        if (dialogueData == null)
        {
            throw new ArgumentNullException(
                nameof(dialogueData),
                "DialogueDB requires a DialogueDataSO instance.");
        }

        try
        {
            dialogueData.ValidateImportedData();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"DialogueDataSO '{dialogueData.name}' failed runtime validation. {exception.Message}",
                exception);
        }

        _preCastingDialogues = CreateDatabase(
            dialogueData.PreCastingDialogues,
            item => item.ID,
            "Pre-CastingDialogue");

        _castingDialogues = CreateDatabase(
            dialogueData.CastingDialogues,
            item => item.ID,
            "CastingDialogue");
    }

    public bool TryGetPreCastingDialogue(int id, out PreCastingDialogueData dialogue)
    {
        return _preCastingDialogues.TryGetValue(id, out dialogue);
    }

    public PreCastingDialogueData GetPreCastingDialogue(int id)
    {
        if (TryGetPreCastingDialogue(id, out PreCastingDialogueData dialogue))
        {
            return dialogue;
        }

        throw CreateNotFoundException("Pre-CastingDialogue", id);
    }

    public bool TryGetCastingDialogue(int id, out CastingDialogueData dialogue)
    {
        return _castingDialogues.TryGetValue(id, out dialogue);
    }

    public CastingDialogueData GetCastingDialogue(int id)
    {
        if (TryGetCastingDialogue(id, out CastingDialogueData dialogue))
        {
            return dialogue;
        }

        throw CreateNotFoundException("CastingDialogue", id);
    }

    private static Dictionary<int, T> CreateDatabase<T>(
        IEnumerable<T> source,
        Func<T, int> keySelector,
        string sheetName)
        where T : class
    {
        var database = new Dictionary<int, T>();

        if (source == null)
        {
            throw new InvalidOperationException(
                $"DialogueDataSO [{sheetName}] list is null. Reimport DialogueData.xlsx.");
        }

        int index = 0;
        foreach (T item in source)
        {
            index++;

            if (item == null)
            {
                throw new InvalidOperationException(
                    $"DialogueDataSO [{sheetName}] item {index} is null. Reimport DialogueData.xlsx.");
            }

            int id = keySelector(item);
            if (id <= 0)
            {
                throw new InvalidOperationException(
                    $"DialogueDataSO [{sheetName}] contains invalid ID {id}. IDs must be greater than zero.");
            }

            if (!database.TryAdd(id, item))
            {
                throw new InvalidOperationException(
                    $"DialogueDataSO [{sheetName}] contains duplicate ID {id}. Check DialogueData.xlsx.");
            }
        }

        return database;
    }

    private static KeyNotFoundException CreateNotFoundException(string sheetName, int id)
    {
        return new KeyNotFoundException(
            $"Dialogue ID {id} was not found in [{sheetName}]. Check DialogueData.xlsx and DialogueDataSO.");
    }
}
