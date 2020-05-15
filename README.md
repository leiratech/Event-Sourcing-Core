# Leira.EventStore
Strongly typed event sourcing framework that useses CosmosDb as a datastore with strong consistency and resiliency.

## About Event Sourcing
Traditionally, developers used to store state in databases, however this can be a problem as it doesn't track what happened to get to that end state. Making data auditing and applications debugging more difficult. Event Sourcing is the concept of storing all events that lead to the current state, which allows you to construct your endstate anytime on the fly.

## Terminologies
### Aggregate
An aggregate is your state object, eg. User, this is your UserAggregate. The aggregate can execute Commands & apply Events.

### Command
A command is the action that the user takes in order to take an action, eg. Signup, ChangePassword ...etc. The command is responsible to validate the current state and that it can take the necessary action, however it doesn't change the state it self. Each command emmit Event(s) that change the state of the aggregate.

### Event
Emitted by Commands, events are units of change against an Aggregate. Each event changes the state of the Aggregate appropriately.

## Features
- Commands Idepmotency (Prevents the same command from running twice.
- Automatic Commands Storage.
- Automatic Events Versioning.
- Automatic Snapshotting.
- Cross Servers Concurrency and Consistency.
- Strong or Loose Consistency options.

# Usage
## Setup
In the framework, there 3 main abstract classes that you need to inherit from.
- Aggregate
- Event
- Command
- Error Class/Struct/Enum which you will use to report errors in command execution

## Prepare
### Create the Error Enum
``` c#
public enum Error
{
  None = 0,
  UserExist = 1,
}
```

### Create the Aggregate
``` c#
using Leira.EventSourcing.Abstracts;

public class User : Aggregate<Error>
{
  public string Name { get; set; }
  public DateTime DateOfBirth { get; set; }
  public DateTime SignupTime { get; set; }
  public string Country { get; set; }
  public bool ProfileSet { get; set; }
}
```

### Create the Command
``` c#
using Leira.EventSourcing.Abstracts

public class SignupUser : Command
{
  public string Name { get; set; }
  public DateTime DateOfBirth { get; set; }
  public string IpAddress { get; set; }
}
```

### Create the Event
``` c#
using Leira.EventSourcing.Abstracts

public class UserSignedup : Event
{
  public string Name { get; set; }
  public DateTime DateOfBirth { get; set; }
  public DateTime SignupTime { get; set; }
  public string Country { get; set; }
}
```

### Expand the aggregate
``` c#
using Leira.EventSourcing.Abstracts;
using Leira.EventSourcing.Interfaces;

public class User : Aggregate<Error>, 
                    IAsyncCommandExecutor<SignupUser>,
                    IAsyncEventHandler<UserSignedup>
{
  public string Name { get; set; }
  public DateTime DateOfBirth { get; set; }
  public DateTime SignupTime { get; set; }
  public string Country { get; set; }
  public bool ProfileSet { get; set; }

  public async Task<CommandResult<TError>> ExecuteCommandAsync(TCommand command)
  {
    // Here we validate our model, does the user exist? can we take the required action?
    if (ProfileSet)
    {
      return new CommandResult(Error.UserExist);
    }
    
    return new CommandResult(Error.None, new UserSignedup(){}
  }

  public async Task ApplyEventAsync(UserSignedup @event)
  {
  }
}
```
