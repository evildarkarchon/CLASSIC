// Models/GameVariables.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using ReactiveUI;

namespace CLASSIC.Models
{
    public class GameVariables : ReactiveObject
    {
        private string _vr = string.Empty;
        private GameId _game = GameId.Fallout4;
        
        public string VR
        { 
            get => _vr;
            set => this.RaiseAndSetIfChanged(ref _vr, value);
        }
        
        public GameId Game
        {
            get => _game;
            set => this.RaiseAndSetIfChanged(ref _game, value);
        }
        
        public string GameName => Game.ToString();
        public string GameNameWithVR => $"{Game}{VR}";
    }
    
    public class GameInfo : ReactiveObject
    {
        private string _gamePath;
        private string _docsPath;
        private string _gameVersion;
        private string _crashgenName;
        private string _xseAcronym;
        
        public string GamePath
        {
            get => _gamePath;
            set => this.RaiseAndSetIfChanged(ref _gamePath, value);
        }
        
        public string DocsPath
        {
            get => _docsPath;
            set => this.RaiseAndSetIfChanged(ref _docsPath, value);
        }
        
        public string GameVersion
        {
            get => _gameVersion;
            set => this.RaiseAndSetIfChanged(ref _gameVersion, value);
        }
        
        public string CrashgenName
        {
            get => _crashgenName;
            set => this.RaiseAndSetIfChanged(ref _crashgenName, value);
        }
        
        public string XSEAcronym
        {
            get => _xseAcronym;
            set => this.RaiseAndSetIfChanged(ref _xseAcronym, value);
        }
    }
}