using System;
using System.Collections.Generic;
using System.Linq;
using BirdGame.Core;
using FriendShapedDistributable;
using Godot;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin.ConfigEditor;

public abstract partial class SearchPopup<TKey> : PopupController
{
    private readonly CachedNode<QuickSearchController> _quickSearchController = new("QuickSearchController");

    private readonly List<TKey> _searchResults = new();
    private Func<TKey, string> _keyToString = x => "KeyToString was not defined!";
    private Action<TKey>? _onChosen;
    private List<TKey> _possibleSearchResults = new();

    private QuickSearchController QuickSearchController => _quickSearchController.Get(this);

    public void Initialize(Action<TKey> onChosen, TKey startingKey, List<TKey> allPossibleResults,
        Func<TKey, string> keyToString, bool canSetNull, string? startingTextBuffer = null)
    {
        _keyToString = keyToString;
        _possibleSearchResults = allPossibleResults;
        QuickSearchController.Initialize(startingTextBuffer ?? keyToString(startingKey), OnTextChanged, OnSubmit,
            canSetNull,
            () => Choose(GetEmptyValue()));
        _onChosen = onChosen;

        OnInitialize(startingKey);
    }

    protected override void AfterInput(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed(StringNameCache.UiCancel))
        {
            ClosePopup();
        }
    }

    protected abstract void OnInitialize(TKey startingKey);

    protected abstract TKey GetEmptyValue();

    protected void Choose(TKey value)
    {
        _onChosen?.Invoke(value);
        ClosePopup();
    }

    private void OnSubmit(string newText)
    {
        if (string.IsNullOrWhiteSpace(newText))
        {
            return;
        }

        var item = _searchResults.FirstOrDefault();

        if (item != null)
        {
            Choose(item);
        }
    }

    private void OnTextChanged(string query)
    {
        _searchResults.Clear();

        foreach (var key in FuzzyUtilities.Rank(query, _possibleSearchResults, _keyToString))
        {
            _searchResults.Add(key);
        }

        if (_searchResults.Count == 0)
        {
            _searchResults.AddRange(_possibleSearchResults);
        }

        QuickSearchController.CreateNodesUpToIndex(_possibleSearchResults.Count - 1);

        QuickSearchController.UpdateResults(Math.Max(_searchResults.Count, QuickSearchController.ResultNodeCount), i =>
        {
            if (!_searchResults.IsValidIndex(i))
            {
                return new SearchResult(); // Exists will be false
            }

            var key = _searchResults[i];
            return new SearchResult(true, _keyToString(key), () => Choose(key), key);
        });
    }

    public override Control? GetDefaultFocusNode()
    {
        return QuickSearchController.GetDefaultFocusNode();
    }
}