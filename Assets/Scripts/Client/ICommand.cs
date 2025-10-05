public interface ICommand
{
    string ActionID { get; }
    void Execute();
    void Undo();
    void CleanUp();
}