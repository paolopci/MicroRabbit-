using MicroRabbitMQ.Domain.Core.Events;


namespace MicroRabbitMQ.Domain.Core.Bus
{
    public interface IEventHandler<in TEvent>:IEventHandler where TEvent : Event
    {
        Task Handle(TEvent @event);
    }

    public interface IEventHandler
    {
    }
}