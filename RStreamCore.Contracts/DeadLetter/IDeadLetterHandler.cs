namespace RStreamCore.Contracts.DeadLetter
{
    public interface IDeadLetterHandler
    {
        Task HandleAsync(DeadLetterMessage message, CancellationToken cancellationToken);
    }
}
