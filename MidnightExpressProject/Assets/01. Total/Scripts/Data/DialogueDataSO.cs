using System;
using System.Collections.Generic;
using UnityEngine;

namespace ExcelData
{
    [Flags]
    public enum EventType
    {
        End =       0b000000000,
        Animation = 0b000000001,
        Camera =    0b000000010,
        Event =     0b000000100,
        Wait =      0b000001000,
        Script =    0b000010000,
        Bubble =    0b000100000,
        Select =    0b001000000,
        Parallel =  0b010000000,
    }

    public enum CameraEventType
    {
        ZoomIn,
        ZoomOut,
        Shake,
        CameraShift,
        FadeIn,
        FadeOut,
    }

    [Serializable]
    public class DialogueClass
    {
        public int ID;
        public EventType EventType; //이벤트 함수
        public float Duration;
        public int NextID;

        public bool IsThisEvent(EventType type) => (EventType & type) == type;
    }

    [Serializable]
    public class ScriptClass
    {
        public int ID;
        public string ShowName;
        public string ImageName;
        public string Script;
    }

    [Serializable]
    public class AnimationClass
    {
        public int ID;
        public string Name;
        public string AnimationName;
    }

    [Serializable]
    public class CameraClass
    {
        public int ID;
        public string Name;
        public CameraEventType Type;
        public float Size;
        public float Duration;
    }

    [Serializable]
    public class BubbleClass
    {
        public int ID;
        public string Name;
        public string Script;
    }

    [Serializable]
    public class SelectClass
    {
        public int ID;
        public string Script;
        public string Name;
        public int ChoiceCount;
    }

    [Serializable]
    public class ParallelClass
    {
        public int ID;
        public float TimeOffset;
    }

    [ExcelAsset(ExcelName = "DialogueData", HeaderRow = 0, DataStartRow = 1, DataStartColumn = 0, AssetPath = "07. ScriptableObjects", LogOnImport = true)]
    public class DialogueDataSO : ScriptableObject
    {
        //변수명은 시트 명으로
        public List<DialogueClass> Dialogue;
        public List<AnimationClass> Animation;
        public List<ScriptClass> Script;
        public List<CameraClass> Camera;
        public List<BubbleClass> Bubble;
        public List<SelectClass> Select;
        public List<ParallelClass> Parallel;
    }
}