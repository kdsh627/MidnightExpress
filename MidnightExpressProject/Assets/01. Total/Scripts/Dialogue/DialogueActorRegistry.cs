using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DialogueActorRegistry : MonoBehaviour
{
    [SerializeField] private List<DialogueActor> _actors = new List<DialogueActor>();

    private readonly Dictionary<string, DialogueActor> _actorsByName =
        new Dictionary<string, DialogueActor>(StringComparer.Ordinal);

    private bool _isInitialized;

    public event Action<DialogueActor> ActorSelected;

    public IReadOnlyList<DialogueActor> Actors => _actors;

    public void Initialize()
    {
        if (_isInitialized)
        {
            return;
        }

        PopulateActorsWhenEmpty();
        _actorsByName.Clear();

        foreach (var actor in _actors)
        {
            if (actor == null)
            {
                throw new InvalidOperationException($"DialogueActorRegistry '{name}' contains a missing actor reference.");
            }

            actor.ValidateConfiguration();
            var key = NormalizeName(actor.CharacterName);
            if (!_actorsByName.TryAdd(key, actor))
            {
                throw new InvalidOperationException(
                    $"DialogueActorRegistry '{name}' contains duplicate character name '{actor.CharacterName}'.");
            }

            actor.Selected += HandleActorSelected;
            actor.Bubble.HideImmediate();
            actor.PrepareAppearance();
        }

        _isInitialized = true;
    }

    public void Shutdown()
    {
        if (!_isInitialized)
        {
            return;
        }

        foreach (var actor in _actors)
        {
            if (actor == null)
            {
                continue;
            }

            actor.Selected -= HandleActorSelected;
            if (actor.Bubble != null)
            {
                actor.Bubble.HideImmediate();
            }
        }

        _actorsByName.Clear();
        _isInitialized = false;
    }

    public bool TryGetActor(string characterName, out DialogueActor actor)
    {
        return _actorsByName.TryGetValue(NormalizeName(characterName), out actor);
    }

    public bool TrySetPreCastingEventId(string characterName, int eventId)
    {
        if (!TryGetActor(characterName, out var actor))
        {
            return false;
        }

        actor.SetPreCastingEventId(eventId);
        return true;
    }

    private void HandleActorSelected(DialogueActor actor)
    {
        ActorSelected?.Invoke(actor);
    }

    private void PopulateActorsWhenEmpty()
    {
        if (_actors.Count > 0)
        {
            return;
        }

        var discoveredActors = FindObjectsByType<DialogueActor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var actor in discoveredActors)
        {
            if (actor.gameObject.scene == gameObject.scene)
            {
                _actors.Add(actor);
            }
        }

        _actors.Sort((left, right) =>
            left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));
    }

    private void OnDestroy()
    {
        Shutdown();
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Normalize(NormalizationForm.FormC);
    }
}
