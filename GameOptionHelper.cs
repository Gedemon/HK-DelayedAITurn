using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Amplitude;
using Amplitude.Framework;
using Amplitude.Framework.Localization;
using Amplitude.Framework.Options;
using Amplitude.Mercury.Data.GameOptions;
using Amplitude.Mercury.Options;
using Amplitude.Mercury.UI;
using Amplitude.UI;
using UnityEngine;

namespace HumankindModTool
{
    public class GameOptionInfo
    {
        public string Key { get; set; }
        public string GroupKey { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string DefaultValue { get; set; }
        public UIControlType ControlType { get; set; }
        public List<GameOptionStateInfo> States { get; set; } = new List<GameOptionStateInfo>();

    }
    public class GameOptionStateInfo
    {
        public string Value { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
    public static class GameOptionHelper
    {
        static IGameOptionsService _gameOptions;
        static IGameOptionsService GameOptions
        {
            get
            {
                if (_gameOptions == null)
                {
                    _gameOptions = Services.GetService<IGameOptionsService>();
                }
                return _gameOptions;
            }
        }

        public static string GetGameOption(GameOptionInfo info)
        {
            return GameOptions.GetOption(new StaticString(info.Key)).CurrentValue;
        }
        public static bool CheckGameOption(GameOptionInfo info, string checkValue, bool caseSensitive = false)
        {
            var val = GetGameOption(info);
            if (caseSensitive)
            {
                return val == checkValue;
            }
            return val?.ToLower() == checkValue?.ToLower();
        }
        public static void Initialize(params GameOptionInfo[] Options)
        {
            var gameOptions = Databases.GetDatabase<GameOptionDefinition>();
            var uiMappers = Databases.GetDatabase<UIMapper>();
            var localizedStrings = Databases.GetDatabase<LocalizedStringElement>();

            foreach (var optionVal in Options)
            {
                var lastKey = gameOptions.Max(x => x.Key);
                var gameOptionName = optionVal.Key;
                var option = ScriptableObject.CreateInstance<GameOptionDefinition>();
                //option.IsEditableInGame = true;
                option.CanBeRandomized = false;
                option.Key = ++lastKey;
                option.XmlSerializableName = gameOptionName;
                option.name = gameOptionName;
                option.Default = optionVal.DefaultValue;
                option.States = new OptionState[optionVal.States.Count];
                for (int i = 0; i < option.States.Length; i++)
                {
                    option.States[i] = new OptionState { Value = optionVal.States[i].Value };
                };
                gameOptions.Touch(option);
                localizedStrings.Touch(new LocalizedStringElement()
                {
                    LineId = $"%{gameOptionName}Title",
                    LocalizedStringElementFlag = LocalizedStringElementFlag.None,
                    CompactedNodes = new LocalizedNode[] {
                        new LocalizedNode{ Id= LocalizedNodeType.Terminal, TextValue=optionVal.Title}
                    },
                    TagCodes = new[] { 0 }
                });
                localizedStrings.Touch(new LocalizedStringElement()
                {
                    LineId = $"%{gameOptionName}Description",
                    LocalizedStringElementFlag = LocalizedStringElementFlag.None,
                    CompactedNodes = new LocalizedNode[] {
                        new LocalizedNode{ Id= LocalizedNodeType.Terminal, TextValue=optionVal.Description}
                    },
                    TagCodes = new[] { 0 }
                });
                foreach (var opt in optionVal.States)
                {
                    localizedStrings.Touch(new LocalizedStringElement()
                    {
                        LineId = $"%{gameOptionName}{opt.Value}Title",
                        LocalizedStringElementFlag = LocalizedStringElementFlag.None,
                        CompactedNodes = new LocalizedNode[] {
                            new LocalizedNode{ Id= LocalizedNodeType.Terminal, TextValue=opt.Title }
                        },
                        TagCodes = new[] { 0 }
                    });
                    localizedStrings.Touch(new LocalizedStringElement()
                    {
                        LineId = $"%{gameOptionName}{opt.Value}Description",
                        LocalizedStringElementFlag = LocalizedStringElementFlag.None,
                        CompactedNodes = new LocalizedNode[] {
                            new LocalizedNode{ Id= LocalizedNodeType.Terminal, TextValue=opt.Description }
                        },
                        TagCodes = new[] { 0 }
                    });
                }
                var optionGroupNameField = typeof(OptionsGroupUIMapper).GetField("optionsName", BindingFlags.Instance | BindingFlags.NonPublic);
                var optionMapper = ScriptableObject.CreateInstance<OptionUIMapper>();
                optionMapper.name = gameOptionName;
                optionMapper.XmlSerializableName = gameOptionName;
                optionMapper.OptionFlags = OptionUIMapper.Flags.None;
                optionMapper.ControlType = optionVal.ControlType;
                optionMapper.Title = $"%{gameOptionName}Title";
                optionMapper.Description = $"%{gameOptionName}Description";
                optionMapper.Initialize();
                uiMappers.Touch(optionMapper);
                if (uiMappers.TryGetValue(new StaticString(optionVal.GroupKey), out var paceGroup))
                {
                    var optionGroup = (OptionsGroupUIMapper)paceGroup;
                    var optionsName = (string[])optionGroupNameField.GetValue(optionGroup);
                    optionsName = optionsName.Union(new[] { gameOptionName }).ToArray();
                    optionGroupNameField.SetValue(optionGroup, optionsName);
                    optionGroup.Initialize();
                    uiMappers.Touch(optionGroup);
                }
            }
        }

    }
}
