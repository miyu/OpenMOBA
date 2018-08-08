using System;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Dargon.Commons;
using Dargon.Courier;
using Dargon.Courier.Management.Repl;
using Dargon.Courier.ServiceTier.Server;
using Dargon.Courier.TransportTier.Tcp;
using Dargon.PlayOn.Foundation;
using Dargon.Repl;
using Dargon.Ryu;
using Dargon.Ryu.Modules;
using Dargon.Vox;
using static Dargon.Courier.ServiceTier.Client.CourierClientRmiStatics;

namespace AdventuresInShade.Server {
   public class Program {
      private const int kTcpPort = 21337;

      public static async Task Main(string[] args) {
         if (args.Any(a => a.StartsWith("-v"))) DmiEntryPoint.InitializeLogging(); // enable verbose logging

         // Courier is injected into IoC container via lambda module
         var courierInitModule = LambdaRyuModule.ForRequired(r => CourierBuilder.Create(r)
                                                                                .UseTcpServerTransport(kTcpPort)
                                                                                .BuildAsync()
                                                                                .Result);

         // Init IoC container and get courierFacade declared above.
         var ryuConfiguration = new RyuConfiguration { AdditionalModules = { courierInitModule } };
         var ryu = new RyuFactory().Create(ryuConfiguration);
         var courier = ryu.GetOrActivate<CourierFacade>(); // declared above

         // Register local game management service
         courier.LocalServiceRegistry.RegisterService(typeof(ILocalGameManagementService), ryu.GetOrThrow<LocalGameManagementService>());

         // Run postinit task.
         await PostInitAsync();
      }

      private static async Task PostInitAsync() {
         var client = await CourierBuilder.Create(new RyuContainer(null, null))
                                          .UseTcpClientTransport(IPAddress.Loopback, kTcpPort)
                                          .BuildAsync();

         // Kick off game start
         await SimulateLocalGameStartFromDataCenter(client);

         // Enter REPL
         var dispatcher = new DispatcherCommand("root");
         dispatcher.RegisterDargonManagementInterfaceCommands();
         new ReplCore(dispatcher).Run(new[]{ "use tcp && fetch-mobs" });
      }

      private static async Task<GameCreationResponseDto> SimulateLocalGameStartFromDataCenter(CourierFacade client) {
         while (client.PeerTable.Enumerate().None()) {
            await Task.Delay(100);
         }

         var peer = client.PeerTable.Enumerate().First();
         var gms = client.RemoteServiceProxyContainer.Get<ILocalGameManagementService>(peer);
         var gcr = new GameCreationRequestDto();
         var res = await Async(() => gms.CreateGameServerInstance(gcr));
         Console.WriteLine(res);
         return res;
      }
   }

   public class ServerRyuModule : RyuModule {
      public override RyuModuleFlags Flags => RyuModuleFlags.Default;

      public ServerRyuModule() {
         Required.Singleton<GameFactory>();
         Required.Singleton<LocalGameManagementService>().Implements<ILocalGameManagementService>();
      }
   }

   [AutoSerializable]
   public class GameCreationRequestDto {
   }

   [AutoSerializable]
   public class GameCreationResponseDto {
      public int GameId { get; set; }
      public Guid MultiplayerNetworkingServiceGuid { get; set; }
   }

   public class PlayOnNetworkingVoxTypes : VoxTypes {
      public PlayOnNetworkingVoxTypes() : base(1000000) {
         // Game creation
         var gameCreationBaseId = 0;
         Register<GameCreationRequestDto>(gameCreationBaseId + 0);
         Register<GameCreationResponseDto>(gameCreationBaseId + 1);
      }
   }

   [Guid("90C6A5E6-E555-45CC-90F4-AD3960490D6F")]
   public interface ILocalGameManagementService {
      GameCreationResponseDto CreateGameServerInstance(GameCreationRequestDto request);
   }

   public class LocalGameManagementService : ILocalGameManagementService {
      private readonly GameFactory gameFactory;
      private readonly LocalServiceRegistry localServiceRegistry;
      private int nextGameId = 0;

      public LocalGameManagementService(GameFactory gameFactory, LocalServiceRegistry localServiceRegistry) {
         this.gameFactory = gameFactory;
         this.localServiceRegistry = localServiceRegistry;
      }

      public GameCreationResponseDto CreateGameServerInstance(GameCreationRequestDto request) {
         var gameId = Interlocked.Increment(ref nextGameId);
         var game = gameFactory.Create();

         var replayLogManager = new ReplayLogManager();
         var replayLogService = new ReplayLogService(replayLogManager);

         var multiplayerNetworkingService = new MultiplayerNetworkingServiceProxyDispatcher(game, replayLogService);
         var multiplayerNetworkingServiceGuid = Guid.NewGuid();
         localServiceRegistry.RegisterService(multiplayerNetworkingServiceGuid, typeof(IMultiplayerNetworkingService), multiplayerNetworkingService);

         return new GameCreationResponseDto {
            GameId = gameId,
            MultiplayerNetworkingServiceGuid = multiplayerNetworkingServiceGuid,
         };
      }
   }

   public interface IMultiplayerNetworkingService : IReplayLogService {
   }

   public class MultiplayerNetworkingServiceProxyDispatcher : IMultiplayerNetworkingService {
      private readonly Game game;
      private readonly IReplayLogService replayLogService;

      public MultiplayerNetworkingServiceProxyDispatcher(Game game, IReplayLogService replayLogService) {
         this.game = game;
         this.replayLogService = replayLogService;
      }

      public Game Game => game;

      public byte[][] GetLog(Guid guid, Guid accessToken, int ack) => replayLogService.GetLog(guid, accessToken, ack);
   }
}
