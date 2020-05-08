using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Leira.EventSourcing.Interfaces
{
    public interface IAsyncCommandExecutor<TCommand, TError> where TCommand : ICommand
                                                             where TError : Enum
    {
        public Task<CommandResult<TError>> ExecuteCommandAsync(TCommand command);
    }
}
