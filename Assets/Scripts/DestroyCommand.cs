using UnityEngine;
using System.Collections.Generic;
using System;

public class DestroyCommand : ICommand
{
    public string ActionID { get; private set; }
    private GameObject objectToDestroy;
    private List<GameObject> activePartsList;
    private WebSocketClientManager webSocketClientManager;
    private string targetPartID;

    public DestroyCommand(GameObject target, List<GameObject> activeParts, WebSocketClientManager wsManager)
    {
        ActionID = Guid.NewGuid().ToString();
        objectToDestroy = target;
        activePartsList = activeParts;
        webSocketClientManager = wsManager;

        if (target != null)
        {
            this.targetPartID = target.name;
        }
    }

    public void Execute()
    {
        if (objectToDestroy != null)
        {
            objectToDestroy.SetActive(false);
            activePartsList.Remove(objectToDestroy);

            if (webSocketClientManager != null && !string.IsNullOrEmpty(targetPartID))
            {
                var destroyData = new DestroyActionData
                {
                    actionID = this.ActionID,
                    targetPartID = this.targetPartID
                };
                webSocketClientManager.SendExecuteDestroy(destroyData);
            }
        }
    }

    public void Undo()
    {
        if (objectToDestroy != null)
        {
            objectToDestroy.SetActive(true);
            if (!activePartsList.Contains(objectToDestroy))
            {
                activePartsList.Add(objectToDestroy);
            }
        }
    }

    public void CleanUp() { }
}