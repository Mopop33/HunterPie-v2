﻿using HunterPie.Core.Address.Map;
using HunterPie.Core.Domain;
using HunterPie.Core.Domain.Interfaces;
using HunterPie.Core.Domain.Process;
using HunterPie.Core.Extensions;
using HunterPie.Core.Game.Client;
using HunterPie.Core.Game.Data;
using HunterPie.Core.Game.Data.Schemas;
using HunterPie.Core.Game.Enums;
using HunterPie.Core.Game.Rise.Definitions;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace HunterPie.Core.Game.Rise.Entities
{
    public class MHRPlayer : Scannable, IPlayer, IEventDispatcher
    {
        #region Private
        private int SaveSlotId;
        private string _name;
        private int _stageId;
        private Weapon _weaponId;
        #endregion 

        public string Name
        {
            get => _name;
            private set
            {
                if (value != _name)
                {
                    _name = value;
                    FindPlayerSaveSlot();
                    this.Dispatch(value is ""
                        ? OnLogout
                        : OnLogin);

                }
            }
        }

        public int HighRank { get; private set; }

        public int StageId
        {
            get => _stageId;
            private set
            {
                if (value != _stageId)
                {
                    _stageId = value;
                    this.Dispatch(OnStageUpdate);
                }
            }
        }

        public Weapon WeaponId
        {
            get => _weaponId;
            private set
            {
                if (value != _weaponId)
                {
                    _weaponId = value;
                    this.Dispatch(OnWeaponChange);
                }
            }
        }

        public bool InHuntingZone => StageId >= 200 || StageId == 5;

        public List<IPartyMember> Party { get; } = new();

        private Dictionary<string, IAbnormality> abnormalities = new();
        public IReadOnlyCollection<IAbnormality> Abnormalities => abnormalities.Values;

        public MHRWirebug[] Wirebugs { get; } = { new(), new(), new() };

        public event EventHandler<EventArgs> OnLogin;
        public event EventHandler<EventArgs> OnLogout;
        public event EventHandler<EventArgs> OnHealthUpdate;
        public event EventHandler<EventArgs> OnStaminaUpdate;
        public event EventHandler<EventArgs> OnDeath;
        public event EventHandler<EventArgs> OnActionUpdate;
        public event EventHandler<EventArgs> OnStageUpdate;
        public event EventHandler<EventArgs> OnVillageEnter;
        public event EventHandler<EventArgs> OnVillageLeave;
        public event EventHandler<EventArgs> OnAilmentUpdate;
        public event EventHandler<EventArgs> OnWeaponChange;
        public event EventHandler<IAbnormality> OnAbnormalityStart;
        public event EventHandler<IAbnormality> OnAbnormalityEnd;
        public event EventHandler<MHRWirebug[]> OnWirebugsRefresh;
        
        public MHRPlayer(IProcessManager process) : base(process) { }

        // TODO: Add DTOs for middlewares

        [ScannableMethod]
        private void ScanStageData()
        {
            long stageAddress = _process.Memory.Read(
                AddressMap.GetAbsolute("STAGE_ADDRESS"), 
                AddressMap.Get<int[]>("STAGE_OFFSETS")
            );

            // TODO: Transform this into a structure instead of an array
            int[] stageIds = _process.Memory.Read<int>(stageAddress + 0x60, 5);

            bool isVillage = stageIds[0] == 4;
            bool isMainMenu = stageIds[0] == 0;

            int villageId = stageIds[1];
            int huntId = stageIds[4];

            int zoneId = isMainMenu switch
            {
                true => -1,
                false => isVillage
                ? villageId
                : huntId + 200
            };

            StageId = zoneId;
        }

        [ScannableMethod]
        private void ScanPlayerSaveData()
        {
            if (StageId == -1 || StageId == 199)
            {
                Name = "";
                return;
            }

            long currentPlayerSaveAddress = _process.Memory.Read(
                AddressMap.GetAbsolute("CHARACTER_ADDRESS"),
                AddressMap.Get<int[]>("CHARACTER_OFFSETS")
            );

            long namePtr = _process.Memory.Read<long>(currentPlayerSaveAddress);
            int nameLength = _process.Memory.Read<int>(namePtr + 0x10);
            string name = _process.Memory.Read(namePtr + 0x14, (uint)(nameLength * 2), encoding: Encoding.Unicode);

            Name = name;
        }

        private void FindPlayerSaveSlot()
        {
            if (StageId == -1 || StageId == 199)
            {
                Name = "";
                SaveSlotId = -1;
                return;
            }

            long currentPlayerSaveAddress = _process.Memory.Read(
                AddressMap.GetAbsolute("CHARACTER_ADDRESS"),
                AddressMap.Get<int[]>("CHARACTER_OFFSETS")
            );
            long namePtr = _process.Memory.Read<long>(currentPlayerSaveAddress);

            long saveAddress = _process.Memory.Read(
                AddressMap.GetAbsolute("SAVE_ADDRESS"), 
                AddressMap.Get<int[]>("SAVE_OFFSETS")
            );

            for (int i = 0; i < 3; i++)
            {
                int[] nameOffsets = { i * 8 + 0x20, 0x10 };

                long saveNamePtr = _process.Memory.Deref<long>(saveAddress, nameOffsets);

                if (saveNamePtr == namePtr)
                {
                    SaveSlotId = i;
                    return;
                }
            }
        }

        [ScannableMethod]
        private void ScanPlayerLevel()
        {
            if (SaveSlotId < 0)
                return;

            long saveAddress = _process.Memory.Read(
                AddressMap.GetAbsolute("SAVE_ADDRESS"),
                AddressMap.Get<int[]>("SAVE_OFFSETS")
            );

            int[] levelOffsets = { SaveSlotId * 8 + 0x20, 0x18 };

            int level = _process.Memory.Deref<int>(saveAddress, levelOffsets);

            HighRank = level;
        }

        [ScannableMethod]
        private void ScanPlayerWeaponData()
        {
            long weaponIdPtr = _process.Memory.Read(
                AddressMap.GetAbsolute("WEAPON_ADDRESS"),
                AddressMap.Get<int[]>("WEAPON_OFFSETS")    
            );

            int weaponId = _process.Memory.Read<int>(weaponIdPtr + 0x8C);

            // Why can't capcom keep the same ids for weapons in all their games? :tired:
            WeaponId = weaponId switch
            {
                0 => Weapon.Greatsword,
                1 => Weapon.SwitchAxe,
                2 => Weapon.Longsword,
                3 => Weapon.LightBowgun,
                4 => Weapon.HeavyBowgun,
                5 => Weapon.Hammer,
                6 => Weapon.GunLance,
                7 => Weapon.Lance,
                8 => Weapon.SwordAndShield,
                9 => Weapon.DualBlades,
                10 => Weapon.HuntingHorn,
                11 => Weapon.ChargeBlade,
                12 => Weapon.InsectGlaive,
                13 => Weapon.Bow,
                _ => Weapon.None,
            };
        }

        [ScannableMethod]
        private void ScanPlayerAbnormalitiesCleanup()
        {
            long debuffsPtr = _process.Memory.Read(
                AddressMap.GetAbsolute("ABNORMALITIES_ADDRESS"),
                AddressMap.Get<int[]>("DEBUFF_ABNORMALITIES_OFFSETS")
            );

            if (!InHuntingZone || debuffsPtr == 0)
                ClearAbnormalities();
        }

        [ScannableMethod]
        private void ScanPlayerConsumableAbnormalities()
        {
            if (!InHuntingZone)
                return;

            long consumableBuffs = _process.Memory.Read(
                AddressMap.GetAbsolute("ABNORMALITIES_ADDRESS"), 
                AddressMap.Get<int[]>("CONS_ABNORMALITIES_OFFSETS")
            );

            if (consumableBuffs == 0)
                return;

            AbnormalitySchema[] consumableSchemas = AbnormalityData.GetAllAbnormalitiesFromCategory(AbnormalityData.Consumables);

            foreach (AbnormalitySchema schema in consumableSchemas)
            {
                int abnormSubId = schema.DependsOn switch
                {
                    0 => 0,
                    _ => _process.Memory.Read<int>(consumableBuffs + schema.DependsOn)
                };

                MHRConsumableStructure abnormality = new();

                if (abnormSubId == schema.WithValue)
                    abnormality = _process.Memory.Read<MHRConsumableStructure>(consumableBuffs + schema.Offset);

                abnormality.Timer /= AbnormalityData.TIMER_MULTIPLIER;

                HandleAbnormality<MHRConsumableAbnormality, MHRConsumableStructure>(schema, abnormality.Timer, abnormality);
            }
        }

        [ScannableMethod]
        private void ScanPlayerDebuffAbnormalities()
        {
            // Only scan in hunting zone due to invalid pointer when not in a hunting zone...
            if (!InHuntingZone)
                return;

            long debuffsPtr = _process.Memory.Read(
                AddressMap.GetAbsolute("ABNORMALITIES_ADDRESS"), 
                AddressMap.Get<int[]>("DEBUFF_ABNORMALITIES_OFFSETS")
            );

            if (debuffsPtr == 0)
                return;

            AbnormalitySchema[] debuffSchemas = AbnormalityData.GetAllAbnormalitiesFromCategory(AbnormalityData.Debuffs);
            
            foreach (AbnormalitySchema schema in debuffSchemas)
            {
                int abnormSubId = schema.DependsOn switch
                {
                    0 => 0,
                    _ => _process.Memory.Read<int>(debuffsPtr + schema.DependsOn)
                };

                MHRDebuffStructure abnormality = new();

                // Only read memory if the required sub Id is the required one for this abnormality
                if (abnormSubId == schema.WithValue)
                    abnormality = _process.Memory.Read<MHRDebuffStructure>(debuffsPtr + schema.Offset);

                abnormality.Timer /= AbnormalityData.TIMER_MULTIPLIER;

                HandleAbnormality<MHRDebuffAbnormality, MHRDebuffStructure>(schema, abnormality.Timer, abnormality);
            }
        }

        [ScannableMethod]
        private void ScanPlayerSongAbnormalities()
        {
            if (!InHuntingZone)
                return;

            long songBuffsPtr = _process.Memory.Read(
                AddressMap.GetAbsolute("ABNORMALITIES_ADDRESS"), 
                AddressMap.Get<int[]>("HH_ABNORMALITIES_OFFSETS")
            );

            if (songBuffsPtr == 0)
                return;

            uint songBuffsLength = _process.Memory.Read<uint>(songBuffsPtr + 0x1C);
            long[] songBuffPtrs = _process.Memory.Read<long>(songBuffsPtr + 0x20, songBuffsLength);

            DerefSongBuffs(songBuffPtrs);
        }
        
        [ScannableMethod(typeof(MHRWirebugStructure))]
        private void ScanPlayerWirebugs()
        {
            long wirebugsArrayPtr = _process.Memory.Read(
                AddressMap.GetAbsolute("ABNORMALITIES_ADDRESS"), 
                AddressMap.Get<int[]>("WIREBUG_DATA_OFFSETS")
            );

            if (wirebugsArrayPtr == 0)
            {
                this.Dispatch(OnWirebugsRefresh, Array.Empty<MHRWirebug>());
                return;
            }

            int wirebugsArrayLength = Math.Min(Wirebugs.Length, _process.Memory.Read<int>(wirebugsArrayPtr + 0x1C));
            long[] wirebugsPtrs = _process.Memory.Read<long>(wirebugsArrayPtr + 0x20, (uint)wirebugsArrayLength);

            bool shouldDispatchEvent = false;
            for (int i = 0; i < wirebugsArrayLength; i++)
            {
                long wirebugPtr = wirebugsPtrs[i];
                MHRWirebugStructure data = _process.Memory.Read<MHRWirebugStructure>(wirebugPtr);
                data.Cooldown /= AbnormalityData.TIMER_MULTIPLIER;
                data.MaxCooldown /= AbnormalityData.TIMER_MULTIPLIER;
                
                if (wirebugPtr != Wirebugs[i].Address)
                {
                    shouldDispatchEvent = true;
                    Wirebugs[i].Address = wirebugPtr;
                }

                IUpdatable<MHRWirebugStructure> wirebug = Wirebugs[i];
                wirebug.Update(data);
            }

            // Update last Wirebug with extra data
            MHRWirebugExtrasStructure extraData = _process.Memory.Deref<MHRWirebugExtrasStructure>(
                AddressMap.GetAbsolute("ABNORMALITIES_ADDRESS"), 
                AddressMap.Get<int[]>("WIREBUG_EXTRA_DATA_OFFSETS")
            );
            extraData.Timer /= AbnormalityData.TIMER_MULTIPLIER;
            IUpdatable<MHRWirebugExtrasStructure> lastWirebug = Wirebugs[Wirebugs.Length - 1];
            lastWirebug.Update(extraData);

            if (shouldDispatchEvent)
                this.Dispatch(OnWirebugsRefresh, Wirebugs);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void DerefSongBuffs(long[] buffs)
        {
            int id = 0;
            AbnormalitySchema[] schemas = AbnormalityData.GetAllAbnormalitiesFromCategory(AbnormalityData.Songs);
            foreach (long buffPtr in buffs)
            {
                MHRHHAbnormality abnormality = _process.Memory.Read<MHRHHAbnormality>(buffPtr);
                abnormality.Timer /= AbnormalityData.TIMER_MULTIPLIER;

                AbnormalitySchema maybeSchema = schemas[id];
                
                if (maybeSchema is AbnormalitySchema schema)
                    HandleAbnormality<MHRSongAbnormality, MHRHHAbnormality>(schema, abnormality.Timer, abnormality);

                id++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ClearAbnormalities()
        {
            foreach (IAbnormality abnormality in abnormalities.Values)
                this.Dispatch(OnAbnormalityEnd, abnormality);

            abnormalities.Clear();
        }

        private void HandleAbnormality<T, S>(AbnormalitySchema schema, float timer, S newData) 
            where T : IAbnormality, IUpdatable<S>
            where S : struct
        {
            if (abnormalities.ContainsKey(schema.Id) && timer <= 0)
            {
                IUpdatable<S> abnorm = (IUpdatable<S>)abnormalities[schema.Id];

                abnorm.Update(newData);

                abnormalities.Remove(schema.Id);
                this.Dispatch(OnAbnormalityEnd, (IAbnormality)abnorm);
            }
            else if (abnormalities.ContainsKey(schema.Id) && timer > 0)
            {

                IUpdatable<S> abnorm = (IUpdatable<S>)abnormalities[schema.Id];
                abnorm.Update(newData);
            }
            else if (!abnormalities.ContainsKey(schema.Id) && timer > 0)
            {
                if (schema.Icon == "ICON_MISSING")
                    Logger.Log.Info($"Missing abnormality: {schema.Id}");

                IUpdatable<S> abnorm = (IUpdatable<S>)Activator.CreateInstance(typeof(T), schema);

                abnormalities.Add(schema.Id, (IAbnormality)abnorm);
                abnorm.Update(newData);
                this.Dispatch(OnAbnormalityStart, (IAbnormality)abnorm);
            }
        }
    }
}
