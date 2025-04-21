using MediatR;
using MicroRabbitMQ.Domain.Core.Bus;
using MicroRabbitMQ.Domain.Core.Commands;
using MicroRabbitMQ.Domain.Core.Events;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using RabbitMQ.Client.Events;


namespace MicroRabbitMQ.Infra.Bus
{
    public sealed class RabbitMQBus : IEventBus
    {
        private readonly IMediator _mediator;
        private readonly List<Type> _eventTypes;
        private readonly Dictionary<string, List<Type>> _handlers;

        public RabbitMQBus(IMediator mediator)
        {
            _mediator = mediator;
            _eventTypes = new List<Type>();
            _handlers = new Dictionary<string, List<Type>>();
        }

        public Task SendCommand<T>(T command) where T : Command
        {
            return _mediator.Send(command);
        }

        public void Publish<T>(T @event) where T : Event
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                var eventName = @event.GetType().Name;
                channel.QueueDeclare(eventName, false, false, false, null);

                var message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish("", eventName, null, body);

            }
        }

        public void Subscribe<T, TH>() where T : Event where TH : IEventHandler<T>
        {
            // Ottiene il nome del tipo di evento T
            var eventName = typeof(T).Name;
            // Ottiene il tipo del gestore TH
            var handlerType = typeof(TH);

            if (!_eventTypes.Contains(typeof(T)))
            {
                // Check if the event type is already registered; if not, add it to the list of event types.
                if (!_eventTypes.Contains(typeof(T)))
                    _eventTypes.Add(typeof(T));
            }
            // Controlla se il dizionario _handlers non contiene già una chiave per il nome dell'evento (eventName).
            // Se la chiave non esiste, aggiunge una nuova voce con una lista vuota di tipi.
            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<Type>());
            }

            // Verifica se il tipo del gestore (handlerType) è già registrato per l'evento specificato (eventName).
            // Se un gestore dello stesso tipo è già presente, lancia un'eccezione per evitare duplicati.
            if (_handlers[eventName].Any(h => h.GetType() == handlerType))
            {
                throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }

            // Aggiunge il tipo del gestore (handlerType) alla lista dei gestori (_handlers) per l'evento specificato (eventName).
            // Questo consente di registrare un gestore per un determinato tipo di evento, in modo che possa essere richiamato
            // quando l'evento viene pubblicato. La lista dei gestori per ogni evento è memorizzata nel dizionario _handlers.
            _handlers[eventName].Add(handlerType);

            StartBasicConsume<T>();
        }

        private void StartBasicConsume<T>() where T : Event
        {
            // Crea una nuova istanza di ConnectionFactory con il nome host impostato su "localhost"
            // e abilita l'elaborazione asincrona dei consumatori.
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                DispatchConsumersAsync = true
            };

            // Crea una connessione al server RabbitMQ utilizzando la factory.
            var connection = factory.CreateConnection();
            // Crea un canale di comunicazione attraverso la connessione.
            var channel = connection.CreateModel();
            var eventName = typeof(T).Name;
            // Dichiara una coda con il nome dell'evento. La coda non è durevole, non è esclusiva,
            // non è auto-cancellata e non ha argomenti aggiuntivi.
            channel.QueueDeclare(eventName, false, false, false, null);

            // Crea un consumatore asincrono per il canale.
            var consumer = new AsyncEventingBasicConsumer(channel);

            // Registra un gestore per l'evento "Received" del consumatore. Questo gestore verrà
            // eseguito ogni volta che un messaggio viene ricevuto nella coda.
            consumer.Received += Consumer_Received;

            // Avvia il consumo dei messaggi dalla coda specificata. I messaggi vengono automaticamente
            // riconosciuti come elaborati (auto-acknowledge) dopo essere stati ricevuti.
            channel.BasicConsume(eventName, true, consumer);
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            // Ottiene il nome dell'evento dal corpo del messaggio ricevuto.
            var eventName = e.RoutingKey; // Use RoutingKey instead of typeof(T).Name
            var message = Encoding.UTF8.GetString(e.Body.ToArray()); // Convert ReadOnlyMemory<byte> to byte[] using ToArray()

            try
            {
                await ProcessEvent(eventName, message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
            }
        }

        private async Task ProcessEvent(string eventName, string message)
        {
            if (_handlers.ContainsKey(eventName))
            {
                var subscriptions = _handlers[eventName];
                foreach (var subscription in subscriptions)
                {
                    var handler = Activator.CreateInstance(subscription);
                    var eventType = _eventTypes.FirstOrDefault(e => e.Name == eventName);
                    var @event = JsonConvert.DeserializeObject(message, eventType);
                    var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
                    await (Task)concreteType.GetMethod("Handle").Invoke(handler, new[] { @event });
                }
            }
        }
    }
}
