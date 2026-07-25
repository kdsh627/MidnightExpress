using System;
using System.Collections.Generic;
using ExcelData;
using VContainer;

public class DialogueDB
{
    private Dictionary<Type, object> _databases = new();

    [Inject]
    public DialogueDB(DialogueDataSO dialogueData)
    {
        RegisterDatabase<DialogueClass>(dialogueData.Dialogue, item => item.ID);
        RegisterDatabase<CameraClass>(dialogueData.Camera, item => item.ID);
        RegisterDatabase<AnimationClass>(dialogueData.Animation, item => item.ID);
        RegisterDatabase<ScriptClass>(dialogueData.Script, item => item.ID);
        RegisterDatabase<BubbleClass>(dialogueData.Bubble, item => item.ID);
        RegisterDatabase<SelectClass>(dialogueData.Select, item => item.ID);
        RegisterDatabase<ParallelClass>(dialogueData.Parallel, item => item.ID);
    }

    private void RegisterDatabase<T>(IEnumerable<T> sourceList, Func<T, int> keySelector)
    {
        if (sourceList == null) return;

        Dictionary<int, T> newDictionary = new Dictionary<int, T>();

        foreach (T item in sourceList)
        {
            int key = keySelector(item);

            newDictionary.Add(key, item);
        }

        _databases.Add(typeof(T), newDictionary);
    }

    public T GetData<T>(int id) where T : class
    {
        Type type = typeof(T);

        if (_databases.TryGetValue(type, out object db))
        {
            Dictionary<int, T> typedDict = (Dictionary<int, T>)db;

            if (typedDict.TryGetValue(id, out T value))
            {
                return value;
            }
        }

        return null;
    }
}
