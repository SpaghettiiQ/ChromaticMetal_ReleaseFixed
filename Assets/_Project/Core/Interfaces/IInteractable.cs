namespace _Project.Core.Interfaces
{
    using UnityEngine;

    public interface IInteractable
    {
        string GetPromptSuffix();
        
        // Pass the interactor's GameObject so the object can query for specific components
        bool CanInteract(GameObject interactor);
        
        // Called strictly by the Server
        void Interact(GameObject interactor);
    }
}