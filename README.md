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
The User aggregate must inherit from Aggregate, which means you have to also initialize the constructor. This is simple, everything is done using dependency injection and you don't have to pass those parameters. In reality, you don't even have to create an instance of your Aggregate as it will be created for you. If you need additional parameters in the constructor, add them, we will explain this in details further below.
``` c#
 public class User : Aggregate<Error>
    {
        public User(string aggregateId, ConsistencyRestriction consistencyOption, Container eventsContainer, Container snapshotsContainer, Container commandsContainer) : base(aggregateId, consistencyOption, eventsContainer, snapshotsContainer, commandsContainer)
        {
        }

        public string Name { get; set; }
        public DateTime DateOfBirth { get; set; }
        public DateTime SignupTime { get; set; }
        public string Country { get; set; }
        public bool ProfileSet { get; set; }
    }
```

### Create the Command
``` c#
public class SignupUser : Command
{
  public string Name { get; set; }
  public DateTime DateOfBirth { get; set; }
  public string IpAddress { get; set; }
}
```

### Create the Event
``` c#
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
public class User : Aggregate<Error>,
                    IAsyncCommandExecutor<SignupUser, Error>,
                    IAsyncEventHandler<UserSignedup>
{

    public User(string aggregateId, ConsistencyRestriction consistencyOption, Container eventsContainer, Container snapshotsContainer, Container commandsContainer) : base(aggregateId, consistencyOption, eventsContainer, snapshotsContainer, commandsContainer)
    {
    }

    public string Name { get; set; }
    public DateTime DateOfBirth { get; set; }
    public DateTime SignupTime { get; set; }
    public string Country { get; set; }
    public bool ProfileSet { get; set; }

    public async Task<CommandResult<Error>> ExecuteCommandAsync(SignupUser command)
    {
        if (this.ProfileSet)
        {
            return new CommandResult<Error>(Error.UserExist);
        }

        return new CommandResult<Error>(Error.None, new UserSignedup()
        {
            // Only fill your own properties, all inherited properties from Event will be overwritten by the framework.
            Name = command.Name,
            SignupTime = DateTime.UtcNow,
            DateOfBirth = command.DateOfBirth,
            //Country = await FindCountryFromIp(command.IpAddress).ConfigureAwait(false);

        }) ;

    }

    public async Task ApplyEventAsync(UserSignedup @event)
    {
        // Here we simply apply the changes.
        Name = @event.Name;
        SignupTime = @event.SignupTime;
        DateOfBirth = @event.DateOfBirth;
        //... etc
    }
}
```

### Linking Things Together
Now that all of our classes are setup, we can use our aggregate. It is recommended that you use dependency injection and configure.
First we need the EventStoreClient<TError>, when your application is first run, it will automatically create all the necessary collections and databases (1 database and 3 collections, using 400 RU/S shared).
 
 If you want to create the Database and Collections yourself, you MUST add "sequenceNumber" as a unique constraint in the events Collection ONLY.

In *startup.cs*, add the following.
``` c#
services.AddSingleton(sp => new EventStoreClient<Error>(sp, new ConfigurationOptions("https://{cosmosDbAccount}.documents.azure.com:443/", "{CosmosDbAccessKey}", "cosmosDbDatabaseName")));
// There are additional constructor default values that you can change, which includes the collection names of Snapshots, Commands and Events. Also it allows you to control your creation of DB and Collections RU/s.
```
### Getting an Aggregate
In any place where the EventStoreClient is injected.
``` c#
var user1 = await eventStoreClient.GetOrCreateAggregateAsync<User>("User1", ConsistencyRestriction.Loose).ConfigureAwait(false);
// Additionally, you can pass any additional parameters to the constructor by passing the "params object[] customParameters". If your Custom Parameters in the constructor are injected using dependency injection, the framework will automatically load them. Remember to ONLY inject Singletons. The aggregate is a long living object and injecting Transeint or Scoped may result in problems.
// Now we got the signal from the user to signup. Let's sign them up.

var result = await user1.ExecuteCommandAsync(new SignupUser()
  {
      Name = objectFromFrontend.Name,
      IpAddress = objectFromFrontend.IpAddress,
      DateOfBirth = objectFromFrontend.DateOfBirth,
      Id = Guid.NewGuid().ToString() // Assigning an ID enables Idemptency Check. However this value MUST come from your Frontend. If the Id is not set, the command will not be saved in CosmosDb. This prevents (forexample) the same command from executing twice when the user clicks a button again instead of waiting.
  }).ConfigureAwait(false);
```

The 'result' object contains 3 important properties.
* `public TError CommandError { get; set; }` This is your Error that you generated while executing the command.
* `public Error EventSourcingError { get; internal set; }` This is your Error from the framework:
  * `None` indicates success.
  * `IdempotencyFailure` indicates that the same command has been executed before and if events persisted, they were reversed.
  * `ConsistencyConflict` If you chose `ConsistencyRestriction.Strict`. Having this result means that another command changed the state of the aggregate (Even if the change happened on another server) and now the operation is invalid and reversed.
* `public IEnumerable<Event> Events { get; set; }` These are the events your execution emitted, you can take these and send them over ServiceBus, RabbitMQ...etc for further async processing.

And we are done! simple, yet effective. The framework will not return unless: 
* Command and Events and Snapshot fully persisted in CosmosDb or;
* Failure is recoverable and will continue to retry until successful or reversed;
* Failure cannot be recovered from automatically (Due to consistency level).

For feedback, questions and bugs, please open a new Issue.
