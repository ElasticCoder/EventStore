using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Tcp;
using NUnit.Framework;
using System;
using EventStore.Core.Authentication;
using EventStore.Core.Messaging;
using EventStore.Core.Tests.Authentication;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using System.Text;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Services;

namespace EventStore.Core.Tests.Services.Transport.Tcp
{
    [TestFixture]
    public class TcpClientDispatcherTests
    {
        private readonly NoopEnvelope _envelope = new NoopEnvelope();
        private const byte _version = (byte)ClientVersion.V1;

        private ClientTcpDispatcher _dispatcher;
        private TcpConnectionManager _connection;

        [OneTimeSetUp]
        public void Setup()
        {
            _dispatcher = new ClientTcpDispatcher();

            var dummyConnection = new DummyTcpConnection();
            _connection = new TcpConnectionManager(
                Guid.NewGuid().ToString(), TcpServiceType.External, new ClientTcpDispatcher(),
                InMemoryBus.CreateTest(), dummyConnection, InMemoryBus.CreateTest(), new InternalAuthenticationProvider(
                                new Core.Helpers.IODispatcher(InMemoryBus.CreateTest(), new NoopEnvelope()), new StubPasswordHashAlgorithm(), 1),
                TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), (man, err) => { });
        }

        [Test]
        public void when_unwrapping_message_that_does_not_have_version1_unwrapper_should_use_version2_unwrapper()
        {
            var dto = new TcpClientMessageDto.DeleteStream("test-stream", 2, true, false);
            var package = new TcpPackage(TcpCommand.DeleteStream, Guid.NewGuid(), dto.Serialize());

            var msg = _dispatcher.UnwrapPackage(package, _envelope, SystemAccount.Principal, "", "", _connection, _version) as ClientMessage.DeleteStream;
            Assert.IsNotNull(msg);
        }

        [Test]
        public void when_wrapping_message_that_does_not_have_version1_wrapper_should_use_version2_wrapper()
        {
            var msg = new ClientMessage.DeleteStream(Guid.NewGuid(), Guid.NewGuid(), _envelope, true, "test-stream", 2, false, SystemAccount.Principal);
            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package");
            Assert.AreEqual(TcpCommand.DeleteStream, package.Value.Command);

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.DeleteStream>();
            Assert.IsNotNull(dto, "DTO");
        }

        [Test]
        public void when_wrapping_read_all_events_forward_completed_with_deleted_event_should_downgrade_version()
        {
            var events = new ResolvedEvent[] {
                ResolvedEvent.ForUnresolvedEvent(CreateDeletedEventRecord(), 0),
            };
            var msg = new ClientMessage.ReadAllEventsForwardCompleted(Guid.NewGuid(), ReadAllResult.Success, "", events,
                                                                      new StreamMetadata(), true, 10, new TFPos(0,0),
                                                                      new TFPos(200,200), new TFPos(0,0), 100);

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.ReadAllEventsForwardCompleted, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.ReadAllEventsCompleted>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(1, dto.Events.Count(), "Number of events");

            Assert.AreEqual(int.MaxValue, dto.Events[0].Event.EventNumber, "Event Number");
        }

        [Test]
        public void when_wrapping_read_all_events_forward_completed_with_link_to_deleted_event_should_downgrade_version()
        {
            var events = new ResolvedEvent[] {
                ResolvedEvent.ForResolvedLink(CreateLinkEventRecord(), CreateDeletedEventRecord(), 100)
            };
            var msg = new ClientMessage.ReadAllEventsForwardCompleted(Guid.NewGuid(), ReadAllResult.Success, "", events,
                                                                      new StreamMetadata(), true, 10, new TFPos(0,0),
                                                                      new TFPos(200,200), new TFPos(0,0), 100);

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.ReadAllEventsForwardCompleted, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.ReadAllEventsCompleted>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(1, dto.Events.Count(), "Number of events");

            Assert.AreEqual(0, dto.Events[0].Event.EventNumber, "Event Number");
            Assert.AreEqual(int.MaxValue, dto.Events[0].Link.EventNumber, "Link Event Number");
        }

        [Test]
        public void when_wrapping_read_all_events_backward_completed_with_deleted_event_should_downgrade_version()
        {
            var events = new ResolvedEvent[] {
                ResolvedEvent.ForUnresolvedEvent(CreateDeletedEventRecord(), 0),
            };
            var msg = new ClientMessage.ReadAllEventsBackwardCompleted(Guid.NewGuid(), ReadAllResult.Success, "", events,
                                                                       new StreamMetadata(), true, 10, new TFPos(0,0),
                                                                       new TFPos(200,200), new TFPos(0,0), 100);

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.ReadAllEventsBackwardCompleted, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.ReadAllEventsCompleted>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(1, dto.Events.Count(), "Number of events");

            Assert.AreEqual(int.MaxValue, dto.Events[0].Event.EventNumber, "Event Number");
        }

        [Test]
        public void when_wrapping_read_all_events_backward_completed_with_link_to_deleted_event_should_downgrade_version()
        {
            var events = new ResolvedEvent[] {
                ResolvedEvent.ForResolvedLink(CreateLinkEventRecord(), CreateDeletedEventRecord(), 100)
            };
            var msg = new ClientMessage.ReadAllEventsBackwardCompleted(Guid.NewGuid(), ReadAllResult.Success, "", events,
                                                                       new StreamMetadata(), true, 10, new TFPos(0,0),
                                                                       new TFPos(200,200), new TFPos(0,0), 100);

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.ReadAllEventsBackwardCompleted, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.ReadAllEventsCompleted>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(1, dto.Events.Count(), "Number of events");

            Assert.AreEqual(0, dto.Events[0].Event.EventNumber, "Event Number");
            Assert.AreEqual(int.MaxValue, dto.Events[0].Link.EventNumber, "Link Event Number");
        }

        [Test]
        public void when_wrapping_stream_event_appeared_with_deleted_event_should_downgrade_version()
        {
            var msg = new ClientMessage.StreamEventAppeared(Guid.NewGuid(),
                                                            ResolvedEvent.ForUnresolvedEvent(CreateDeletedEventRecord(), 0));

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.StreamEventAppeared, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.StreamEventAppeared>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(int.MaxValue, dto.Event.Event.EventNumber, "Event Number");
        }

        [Test]
        public void when_wrapping_stream_event_appeared_with_link_to_deleted_event_should_downgrade_version()
        {
            var msg = new ClientMessage.StreamEventAppeared(Guid.NewGuid(),
                                                            ResolvedEvent.ForResolvedLink(CreateLinkEventRecord(), CreateDeletedEventRecord(), 0));

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.StreamEventAppeared, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.StreamEventAppeared>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(0, dto.Event.Event.EventNumber, "Event Number");
            Assert.AreEqual(int.MaxValue, dto.Event.Link.EventNumber, "Link Event Number");
        }


        [Test]
        public void when_wrapping_persistent_subscription_stream_event_appeared_with_deleted_event_should_downgrade_version()
        {
            var msg = new ClientMessage.PersistentSubscriptionStreamEventAppeared(Guid.NewGuid(),
                                                            ResolvedEvent.ForUnresolvedEvent(CreateDeletedEventRecord(), 0));

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.PersistentSubscriptionStreamEventAppeared, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionStreamEventAppeared>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(int.MaxValue, dto.Event.Event.EventNumber, "Event Number");
        }

        [Test]
        public void when_wrapping_persistent_subscription_stream_event_appeared_with_link_to_deleted_event_should_downgrade_version()
        {
            var msg = new ClientMessage.PersistentSubscriptionStreamEventAppeared(Guid.NewGuid(),
                                                            ResolvedEvent.ForResolvedLink(CreateLinkEventRecord(), CreateDeletedEventRecord(), 0));

            var package = _dispatcher.WrapMessage(msg, _version);
            Assert.IsNotNull(package, "Package is null");
            Assert.AreEqual(TcpCommand.PersistentSubscriptionStreamEventAppeared, package.Value.Command, "TcpCommand");

            var dto = package.Value.Data.Deserialize<TcpClientMessageDto.PersistentSubscriptionStreamEventAppeared>();
            Assert.IsNotNull(dto, "DTO is null");
            Assert.AreEqual(0, dto.Event.Event.EventNumber, "Event Number");
            Assert.AreEqual(int.MaxValue, dto.Event.Link.EventNumber, "Link Event Number");
        }


        private EventRecord CreateDeletedEventRecord()
        {
            return new EventRecord(long.MaxValue, LogRecord.DeleteTombstone(0, Guid.NewGuid(), Guid.NewGuid(), "test-stream", long.MaxValue)); 
        }

        private EventRecord CreateLinkEventRecord()
        {
            return new EventRecord(0, LogRecord.Prepare(100, Guid.NewGuid(), Guid.NewGuid(), 0, 0, 
                                            "link-stream", -1, PrepareFlags.SingleWrite | PrepareFlags.Data, SystemEventTypes.LinkTo, 
                                            Encoding.UTF8.GetBytes(string.Format("{0}@test-stream", long.MaxValue)), new byte[0]));
        }
    }
}