using System;
using System.Collections.Generic;
using System.Linq;
using BirdGame.Core;
using FriendShapedDistributable;
using Godot;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin.ConfigEditor;

public partial class QuickSearchController : Control
{
    private readonly CachedNode<Button> _clearButton = new("Stack/HBoxContainer/ClearButton");
    private readonly CachedNode<LineEdit> _lineEdit = new("Stack/LineEdit");
    private readonly CachedNode<Control> _resultsContentRoot = new("Stack/Scroll/Margin/Results");
    private readonly CachedNode<ScrollContainer> _scrollContainer = new("Stack/Scroll");
    private readonly List<SearchResultButton> _searchResultNodes = new();

    private readonly CachedPackedScene<SearchResultButton> _searchResultPrefab =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/SearchResult.tscn");

    private Action? _chooseNull;

    private Action<string>? _onTextChanged;

    private Button ClearButton => _clearButton.Get(this);
    private LineEdit LineEdit => _lineEdit.Get(this);
    private Control ResultsContent => _resultsContentRoot.Get(this);
    private ScrollContainer ScrollContainer => _scrollContainer.Get(this);
    public int ResultNodeCount => ResultsContent.GetChildCount();

    public void Initialize(string startingText, Action<string> onTextChanged,
        LineEdit.TextSubmittedEventHandler onSubmit, bool canChooseNull, Action chooseNull)
    {
        LineEdit.Text = startingText;
        LineEdit.SelectAll();
        LineEdit.CaretColumn = LineEdit.Text.Length;

        ResultsContent.QueueFreeAllChildren();

        LineEdit.TextSubmitted += onSubmit;

        _onTextChanged = onTextChanged;
        _chooseNull = chooseNull;
        ClearButton.Visible = canChooseNull;

        // Initial refresh
        PerfClamps.Start("populate_search_popup");
        OnTextChanged(LineEdit.Text);
        PerfClamps.End("populate_search_popup");
    }

    private void AddSearchResultNode()
    {
        var searchResult = _searchResultPrefab.LoadAndInstantiate();
        _searchResultNodes.Add(searchResult);
        ResultsContent.AddChild(searchResult);
        searchResult.Visible = false;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (LineEdit.IsEditing() && inputEvent.IsActionPressed(StringNameCache.UiDown))
        {
            _searchResultNodes.FirstOrDefault()?.GrabFocus();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!LineEdit.IsEditing() && LineEdit.HasFocus())
        {
            LineEdit.Edit();
            LineEdit.CaretColumn = LineEdit.Text.Length;
        }
    }

    public override void _EnterTree()
    {
        LineEdit.CaretBlink = true;
        LineEdit.CallDeferred(Control.MethodName
            .GrabFocus); // todo: I think we can remove this with the new focus system?
        LineEdit.KeepEditingOnTextSubmit = true;
        LineEdit.TextChanged += OnTextChanged;
        ClearButton.Pressed += OnClear;
    }

    private void OnClear()
    {
        _chooseNull?.Invoke();
    }

    private void OnTextChanged(string enteredText)
    {
        ScrollContainer.ScrollVertical = 0;
        _onTextChanged?.Invoke(enteredText);
    }

    public void CreateNodesUpToIndex(int index)
    {
        if (index < 0)
        {
            return;
        }
        
            while (!_searchResultNodes.IsValidIndex(index))
            {
                AddSearchResultNode();
        }
    }

    public void UpdateResults(int searchResultsCount, Func<int, SearchResult> getResult)
    {
        CreateNodesUpToIndex(searchResultsCount);

        for (var i = 0; i < searchResultsCount; i++)
        {
            var result = getResult(i);
            if (result.Exists)
            {
                _searchResultNodes[i].Visible = true;
                _searchResultNodes[i].Text = result.String;
                _searchResultNodes[i].Initialize(result);
            }
            else
            {
                _searchResultNodes[i].Visible = false;
            }
        }
    }

    public Control GetDefaultFocusNode()
    {
        return LineEdit;
    }
}