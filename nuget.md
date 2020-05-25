[![#](https://img.shields.io/nuget/v/Leira.EventSourcing.svg?style=flat-square)](https://www.nuget.org/packages/Leira.EventSourcing)
![#](https://img.shields.io/github/license/leiratech/Event-Sourcing-Core?style=flat-square)

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
