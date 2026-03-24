namespace RStreamCore.Contracts
{
    public interface IDeadLetterHandler
    {
        Task HandleAsync(DeadLetterMessage message, CancellationToken cancellationToken);
    }
}
