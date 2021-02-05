using System;
using System.Collections.Generic;
using System.Linq;
using Impostor.Api;
using Impostor.Api.Events.Managers;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Customization;
using Impostor.Api.Net;
using Impostor.Api.Net.Messages;
using Impostor.Api.Net.Messages.Rpcs;
using Impostor.Server.Events.Player;
using Impostor.Server.Net.Inner.Objects.Components;
using Impostor.Server.Net.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Impostor.Server.Net.Inner.Objects
{
    internal partial class InnerPlayerControl : InnerNetObject
    {
        private readonly ILogger<InnerPlayerControl> _logger;
        private readonly IEventManager _eventManager;
        private readonly Game _game;

        public InnerPlayerControl(ILogger<InnerPlayerControl> logger, IServiceProvider serviceProvider, IEventManager eventManager, Game game)
        {
            _logger = logger;
            _eventManager = eventManager;
            _game = game;

            Physics = ActivatorUtilities.CreateInstance<InnerPlayerPhysics>(serviceProvider, this, _eventManager, _game);
            NetworkTransform = ActivatorUtilities.CreateInstance<InnerCustomNetworkTransform>(serviceProvider, this, _game);

            Components.Add(this);
            Components.Add(Physics);
            Components.Add(NetworkTransform);

            PlayerId = byte.MaxValue;

            Rpcs[RpcCalls.PlayAnimation] = Rpc.Sync((_, _, reader) =>
            {
                Rpc00PlayAnimation.Deserialize(reader, out var task);
            });

            Rpcs[RpcCalls.CompleteTask] = Rpc.Async(async (sender, _, reader) =>
            {
                Rpc01CompleteTask.Deserialize(reader, out var taskId);
                var task = PlayerInfo.Tasks.ElementAtOrDefault((int)taskId);

                if (task != null)
                {
                    task.Complete = true;
                    await _eventManager.CallAsync(new PlayerCompletedTaskEvent(_game, sender, this, task));
                }
                else
                {
                    _logger.LogWarning($"Client sent {nameof(RpcCalls.CompleteTask)} with a taskIndex that is not in their {nameof(InnerPlayerInfo)}");
                }
            });

            Rpcs[RpcCalls.SyncSettings] = Rpc.Sync(
                (_, _, reader) =>
                {
                    Rpc02SyncSettings.Deserialize(reader, _game.Options);
                }, new Rpc.RpcOptions
                {
                    CheckOwnership = false, RequireHost = true,
                }
            );

            Rpcs[RpcCalls.SetInfected] = Rpc.Async(
                async (_, _, reader) =>
                {
                    Rpc03SetInfected.Deserialize(reader, out var infectedIds);

                    foreach (var infectedId in infectedIds)
                    {
                        var player = _game.GameNet.GameData.GetPlayerById(infectedId);
                        if (player != null)
                        {
                            player.IsImpostor = true;
                        }
                    }

                    if (_game.GameState == GameStates.Starting)
                    {
                        await _game.StartedAsync();
                    }
                },
                new Rpc.RpcOptions
                {
                    RequireHost = true,
                }
            );

            Rpcs[RpcCalls.CheckName] = Rpc.Sync(
                async (sender, target, reader) =>
                {
                    if (!target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CheckName)} to the wrong player");
                    }

                    Rpc05CheckName.Deserialize(reader, out var name);

                    if (name.Length > 10)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CheckName)} with name exceeding 10 characters");
                    }

                    if (string.IsNullOrWhiteSpace(name) || !name.All(TextBox.IsCharAllowed))
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CheckName)} with name containing illegal characters");
                    }

                    if (sender.Client.Name != name)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetName)} with name not matching his name from handshake");
                    }

                    RequestedPlayerName.Enqueue(name);
                },
                new Rpc.RpcOptions
                {
                    TargetType = Rpc.RpcTargetType.Target,
                }
            );

            Rpcs[RpcCalls.SetName] = Rpc.Cancellable(
                async (sender, _, reader) =>
                {
                    Rpc06SetName.Deserialize(reader, out var name);

                    if (sender.IsOwner(this))
                    {
                        if (_game.Players.Any(x => x.Character != null && x.Character != this && x.Character.PlayerInfo.PlayerName == name))
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetName)} with a name that is already used");
                        }

                        if (sender.Client.Name != name)
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetName)} with name not matching his name from handshake");
                        }
                    }
                    else
                    {
                        if (!RequestedPlayerName.Any())
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetName)} for a player that didn't request it");
                        }

                        var expected = RequestedPlayerName.Dequeue();

                        if (_game.Players.Any(x => x.Character != null && x.Character != this && x.Character.PlayerInfo.PlayerName == expected))
                        {
                            var i = 1;
                            while (true)
                            {
                                string text = expected + " " + i;

                                if (_game.Players.All(x => x.Character == null || x.Character == this || x.Character.PlayerInfo.PlayerName != text))
                                {
                                    expected = text;
                                    break;
                                }

                                i++;
                            }
                        }

                        if (name != expected)
                        {
                            _logger.LogWarning($"Client sent {nameof(RpcCalls.SetName)} with incorrect name");
                            await SetNameAsync(expected);
                            return false;
                        }
                    }

                    PlayerInfo.PlayerName = name;

                    return true;
                },
                new Rpc.RpcOptions
                {
                    CheckOwnership = false, RequireHost = true,
                }
            );

            Rpcs[RpcCalls.CheckColor] = Rpc.Sync(
                async (_, target, reader) =>
                {
                    if (!target.IsHost)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CheckColor)} to the wrong player");
                    }

                    var color = reader.ReadByte();

                    if (color > Enum.GetValues<ColorType>().Length)
                    {
                        throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.CheckColor)} with invalid color");
                    }

                    RequestedColorId.Enqueue(color);
                },
                new Rpc.RpcOptions
                {
                    TargetType = Rpc.RpcTargetType.Target,
                }
            );

            Rpcs[RpcCalls.SetColor] = Rpc.Cancellable(
                async (sender, _, reader) =>
                {
                    Rpc08SetColor.Deserialize(reader, out var color);

                    if (sender.IsOwner(this))
                    {
                        if (_game.Players.Any(x => x.Character != null && x.Character != this && x.Character.PlayerInfo.ColorId == (byte)color))
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetColor)} with a color that is already used");
                        }
                    }
                    else
                    {
                        if (!RequestedColorId.Any())
                        {
                            throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.SetColor)} for a player that didn't request it");
                        }

                        var expected = RequestedColorId.Dequeue();

                        while (_game.Players.Any(x => x.Character != null && x.Character != this && x.Character.PlayerInfo.ColorId == expected))
                        {
                            expected = (byte)((expected + 1) % Enum.GetValues<ColorType>().Length);
                        }

                        if ((byte)color != expected)
                        {
                            _logger.LogWarning($"Client sent {nameof(RpcCalls.SetColor)} with incorrect color");
                            await SetColorAsync(expected);
                            return false;
                        }
                    }

                    PlayerInfo.ColorId = (byte)color;

                    return true;
                },
                new Rpc.RpcOptions
                {
                    CheckOwnership = false, RequireHost = true,
                }
            );

            Rpcs[RpcCalls.SetHat] = Rpc.Sync(async (_, _, reader) =>
            {
                Rpc09SetHat.Deserialize(reader, out var hat);
                PlayerInfo.HatId = (byte)hat;
            });

            Rpcs[RpcCalls.SetSkin] = Rpc.Sync(async (_, _, reader) =>
            {
                Rpc10SetSkin.Deserialize(reader, out var skin);
                PlayerInfo.SkinId = (byte)skin;
            });

            Rpcs[RpcCalls.ReportDeadBody] = Rpc.Sync(async (_, _, reader) =>
            {
                Rpc11ReportDeadBody.Deserialize(reader, out var targetId);
            });

            Rpcs[RpcCalls.MurderPlayer] = Rpc.Async(async (sender, _, reader) =>
            {
                if (!PlayerInfo.IsImpostor)
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.MurderPlayer)} as crewmate");
                }

                if (!PlayerInfo.CanMurder(_game))
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.MurderPlayer)} too fast");
                }

                Rpc12MurderPlayer.Deserialize(reader, game, out var target);

                if (target == null)
                {
                    throw new ImpostorCheatException($"Client sent {nameof(RpcCalls.MurderPlayer)} with invalid target");
                }

                PlayerInfo.LastMurder = DateTimeOffset.UtcNow;

                if (!target.PlayerInfo.IsDead)
                {
                    ((InnerPlayerControl)target).Die(DeathReason.Kill);
                    await _eventManager.CallAsync(new PlayerMurderEvent(_game, sender, this, target));
                }
            });

            Rpcs[RpcCalls.SendChat] = Rpc.Async(async (sender, _, reader) =>
            {
                Rpc13SendChat.Deserialize(reader, out var message);

                await _eventManager.CallAsync(new PlayerChatEvent(_game, sender, this, message));
            });

            Rpcs[RpcCalls.StartMeeting] = Rpc.Async(
                async (_, _, reader) =>
                {
                    Rpc14StartMeeting.Deserialize(reader, out var targetId);
                    var deadPlayer = game.GameNet.GameData.GetPlayerById(targetId)?.Controller;

                    await _eventManager.CallAsync(new PlayerStartMeetingEvent(_game, _game.GetClientPlayer(this.OwnerId), this, deadPlayer));
                },
                new Rpc.RpcOptions
                {
                    CheckOwnership = false, RequireHost = true,
                }
            );

            Rpcs[RpcCalls.SetScanner] = Rpc.Sync((_, _, reader) =>
            {
                Rpc15SetScanner.Deserialize(reader, out var on, out var scannerCount);
            });

            Rpcs[RpcCalls.SendChatNote] = Rpc.Sync((_, _, reader) =>
            {
                Rpc16SendChatNote.Deserialize(reader, out var playerId, out var chatNoteType);
            });

            Rpcs[RpcCalls.SetPet] = Rpc.Sync((_, _, reader) =>
            {
                Rpc17SetPet.Deserialize(reader, out var pet);
                PlayerInfo.PetId = (byte)pet;
            });

            Rpcs[RpcCalls.SetStartCounter] = Rpc.Async(async (sender, _, reader) =>
            {
                Rpc18SetStartCounter.Deserialize(reader, out var sequenceId, out var startCounter);

                if (!sender.IsHost && startCounter != -1)
                {
                    throw new ImpostorCheatException("Client tried to set start counter as a non-host");
                }

                if (startCounter != -1)
                {
                    await _eventManager.CallAsync(new PlayerSetStartCounterEvent(_game, sender, this, (byte)startCounter));
                }
            });

            Rpcs[RpcCalls.CustomRpc] = Rpc.Sync((_, _, reader) =>
            {
                var lengthOrShortId = reader.ReadPackedInt32();

                var pluginId = lengthOrShortId < 0
                    ? _game.Host!.Client.ModIdMap[lengthOrShortId]
                    : reader.ReadString(lengthOrShortId);

                var id = reader.ReadPackedInt32();

                // TODO handle custom rpcs
            });
        }

        public bool IsNew { get; private set; }

        public byte PlayerId { get; private set; }

        public InnerPlayerPhysics Physics { get; }

        public InnerCustomNetworkTransform NetworkTransform { get; }

        public InnerPlayerInfo PlayerInfo { get; internal set; }

        internal Queue<string> RequestedPlayerName { get; } = new Queue<string>();

        internal Queue<byte> RequestedColorId { get; } = new Queue<byte>();

        public override bool Serialize(IMessageWriter writer, bool initialState)
        {
            throw new NotImplementedException();
        }

        public override void Deserialize(IClientPlayer sender, IClientPlayer? target, IMessageReader reader, bool initialState)
        {
            if (!sender.IsHost)
            {
                throw new ImpostorCheatException($"Client attempted to send data for {nameof(InnerPlayerControl)} as non-host");
            }

            if (initialState)
            {
                IsNew = reader.ReadBoolean();
            }

            PlayerId = reader.ReadByte();
        }

        internal void Die(DeathReason reason)
        {
            PlayerInfo.IsDead = true;
            PlayerInfo.LastDeathReason = reason;
        }
    }
}
