using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HistoryManager : MonoBehaviour
{
    public static HistoryManager Instance
    {
        get; private set;
    }

    [Header("UI Buttons")]
    [SerializeField] private Button undoButton;
    [SerializeField] private Button redoButton;

    private Stack<ICommand> undoStack = new Stack<ICommand>();
    private Stack<ICommand> redoStack = new Stack<ICommand>();
    private WebSocketClientManager webSocketClientManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    private void Start()
    {
        webSocketClientManager = FindObjectOfType<WebSocketClientManager>();
        UpdateButtons();
    }

    public void ExecuteCommand(ICommand command)
    {
        command.Execute();
        undoStack.Push(command);

        foreach (ICommand redoCommand in redoStack)
        {
            redoCommand.CleanUp();
        }
        redoStack.Clear();

        UpdateButtons();
    }

    public void Undo()
    {
        if (undoStack.Count > 0)
        {
            ICommand command = undoStack.Pop();
            command.Undo();
            redoStack.Push(command);

            if (webSocketClientManager != null)
            {
                webSocketClientManager.SendUndoAction(command.ActionID);
            }
            UpdateButtons();
        }
    }

    public void Redo()
    {
        if (redoStack.Count > 0)
        {
            ICommand command = redoStack.Pop();
            command.Execute();
            undoStack.Push(command);

            if (webSocketClientManager != null)
            {
                webSocketClientManager.SendRedoAction(command.ActionID);
            }
            UpdateButtons();
        }
    }

    public void ClearHistory()
    {
        foreach (ICommand command in undoStack)
            command.CleanUp();
        foreach (ICommand command in redoStack)
            command.CleanUp();

        undoStack.Clear();
        redoStack.Clear();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        if (undoButton != null)
        {
            undoButton.interactable = undoStack.Count > 0;
        }
        if (redoButton != null)
        {
            redoButton.interactable = redoStack.Count > 0;
        }
    }
}