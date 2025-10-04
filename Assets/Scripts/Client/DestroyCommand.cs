using UnityEngine;
using System.Collections.Generic;

public class DestroyCommand : ICommand
{
    private GameObject objectToDestroy;
    private List<GameObject> activePartsList;

    public DestroyCommand(GameObject target, List<GameObject> activeParts)
    {
        objectToDestroy = target;
        activePartsList = activeParts;
    }

    public void Execute()
    {
        if (objectToDestroy != null)
        {
            objectToDestroy.SetActive(false);
            activePartsList.Remove(objectToDestroy);
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

    // Destroy commands don't create new objects, so cleanup is not needed.
    public void CleanUp() { }
}