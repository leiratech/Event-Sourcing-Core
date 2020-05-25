using System.Threading.Tasks;

namespace Leira.EventSourcing.Interfaces
{
    public interface IAsyncCommandExecutor<TCommand, TError>
    {
        public Task<CommandResult<TError>> ExecuteCommandAsync(TCommand command);
    }

    public interface ICommandExecutor<TCommand, TError>
    {
        public CommandResult<TError> ExecuteCommand(TCommand command);
    }
}
