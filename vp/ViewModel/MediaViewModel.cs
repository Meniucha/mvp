﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using vp.Events;
using vp.Messaging;
using vp.Models;
using vp.Services.Media;
using vp.Services.Navigation;
using vp.Services.Playlists;
using vp.Services.Settings;
using vp.UserControls;

namespace vp.ViewModel
{
    /// <summary>
    /// ViewModel for the <see cref="Media"/>
    /// bug? MediaPlayer.RendererOptions.UseLegacyAudioOut = true; if videos not loading
    /// </summary>
    public class MediaViewModel : ViewModelBase
    {
        /// <summary>
        /// <see cref="IMediaService"/> should be initialized using a command from the View
        /// </summary>
        private IMediaService _mediaService;
        private readonly IUserSettings _userSettings;
        private readonly IPlaylistManager _playlistManager;
        private readonly IPageNavigationService _pageNavigationService;
        private double _volume;
        private Playlist _currentPlaylist;
        private Video _currentVideo;
        private bool _isOpenCurrentPlaylistPanel;
        private TimeSpan _currentProgress;

        /// <summary>
        /// Current Media volume
        /// </summary>
        public double Volume
        {
            get => _volume;
            set { Set(() => Volume, ref _volume, value); }
        }

        /// <summary>
        /// Current Position of the media
        /// </summary>
        public TimeSpan Position
        {
            set
            {
                if (_currentVideo != null)
                {
                    _currentVideo.TimeWatched = value;
                }
            }
        }

        public bool IsOpenCurrentPlaylistPanel
        {
            get => _isOpenCurrentPlaylistPanel;
            set => Set(() => IsOpenCurrentPlaylistPanel, ref _isOpenCurrentPlaylistPanel, value);
        }

        public Playlist CurrentPlaylist
        {
            get => _currentPlaylist;
            set => Set(() => CurrentPlaylist, ref _currentPlaylist, value);
        }

        public MediaViewModel(IUserSettings userSettings,
                              IPlaylistManager playlistManager,
                              IPageNavigationService pageNavigationService)
        {
            _userSettings = userSettings;
            _playlistManager = playlistManager;
            _pageNavigationService = pageNavigationService;
            InitializeMediaServiceCommand = new RelayCommand<IMediaService>(OnInitializeMediaService);
            OpenVideoCommand = new RelayCommand<Video>(OnOpenVideo);
            PlayPauseVideoCommand = new RelayCommand(OnPlayPauseVideo);
            PrevVideoCommand = new RelayCommand(OnPrevVideo);
            NextVideoCommand = new RelayCommand(OnNextVideo);
            FullscreenCommand = new RelayCommand(OnFullscreen);
            NavigateToPlaylistPageCommand = new RelayCommand(OnNavigateToPlaylistPage);

            LoadSettings();
            RegisterMessages();

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = ApplicationConstants.AutoSaveInterval;
            timer.Tick += (s, e) => SaveSettings();
            timer.Start();
        }

        private void LoadSettings()
        {
            Volume = _userSettings.Volume;
            CurrentPlaylist = _userSettings.LastPlayedPlaylist;
            IsOpenCurrentPlaylistPanel = _userSettings.IsOpenCurrentPlaylistPanel;
        }

        private void SaveSettings()
        {
            _userSettings.Volume = Volume;
            _userSettings.LastPlayedPlaylist = CurrentPlaylist;
            _userSettings.IsOpenCurrentPlaylistPanel = IsOpenCurrentPlaylistPanel;

            _userSettings.SaveChanges();
        }

        private void RegisterMessages()
        {
            Messenger.Default.Register<PlayVideoMessage>(this, msg =>
            {
                if (msg.Playlist?.Videos != null && msg.Video != null)
                {
                    if (!msg.Playlist.Videos.Contains(msg.Video)) throw new InvalidOperationException($"Video must be a part of the Playlist");

                    this.CurrentPlaylist = msg.Playlist;
                    OnOpenVideo(msg.Video);
                }
            });

            Messenger.Default.Register<PlayPlaylistMessage>(this, msg =>
            {
                if (msg.Playlist?.Videos != null)
                {
                    if (msg.Playlist.CurrentlyPlayingId == null) msg.Playlist.CurrentlyPlayingId = 0;
                    if (msg.Playlist.CurrentlyPlayingId >= msg.Playlist.Videos.Count) throw new IndexOutOfRangeException("Playlist.CurrentlyPlayingId out of range");
                    this.CurrentPlaylist = msg.Playlist;
                    
                    OnOpenVideo(CurrentPlaylist.Videos[msg.Playlist.CurrentlyPlayingId.Value]);
                }
            });
        }

        

        /// <summary>
        /// Initializes the <see cref="IMediaService"/> with the provided <see cref="IMediaService"/>
        /// </summary>
        public RelayCommand<IMediaService> InitializeMediaServiceCommand { get; }

        /// <summary>
        /// Opens the provided <see cref="Video"/> using the <see cref="_mediaService"/>
        /// </summary>
        public RelayCommand<Video> OpenVideoCommand { get; }

        /// <summary>
        /// Play or pauses the <see cref="IMediaService"/> based on <see cref="IMediaService.IsPlaying"/>
        /// </summary>
        public RelayCommand PlayPauseVideoCommand { get; }

        /// <summary>
        /// Plays the previous video if possible
        /// </summary>
        public RelayCommand PrevVideoCommand { get; }

        /// <summary>
        /// Plays the next video if possible
        /// </summary>
        public RelayCommand NextVideoCommand { get; }

        /// <summary>
        /// Sets the <see cref="Application.Current"/>.MainWindow to maximized with <see cref="WindowStyle.None"/>
        /// </summary>
        public RelayCommand FullscreenCommand { get;}

        public RelayCommand NavigateToPlaylistPageCommand { get; }

        private void OnNavigateToPlaylistPage()
        {
            _pageNavigationService.NavigateTo("PlaylistsPage");
        }

        private void OnInitializeMediaService(IMediaService mediaService)
        {
            _mediaService = mediaService;
            _mediaService.MediaEnded += (s, e) => OnNextVideo();
            _mediaService.MediaOpened += (s, e) => OnMediaOpened(e);
        }

        private void OnMediaOpened(MediaOpenedEventArgs e)
        {
            CurrentPlaylist.CurrentlyPlayingId = CurrentPlaylist.Videos.IndexOf(e.OpenedVideo);
            _currentVideo = e.OpenedVideo;
            _currentProgress = _currentVideo.TimeWatched;
            Messenger.Default.Send(new VideoOpenedMessage(e.OpenedVideo));
        }

        private async void OnOpenVideo(Video video)
        {
            //If no null assignment is made then the position binding might set the next videos time watched to 0
            _currentVideo = null;
            await _mediaService.Open(video, video.TimeWatched);
        }

        private async void OnPlayPauseVideo()
        {
            if (_mediaService.IsPlaying)
            {
                await _mediaService.Pause();
            }
            else
            {
                await _mediaService.Play();
            }
        }

        private async void OnNextVideo()
        {
            if (CurrentPlaylist?.CurrentlyPlayingId != null && 
                CurrentPlaylist?.Videos != null && 
                CurrentPlaylist.CurrentlyPlayingId + 1 < CurrentPlaylist.Videos.Count)
            {
                CurrentPlaylist.CurrentlyPlayingId++;
                await _mediaService.Open(CurrentPlaylist.Videos[CurrentPlaylist.CurrentlyPlayingId.Value]);
            }
        }

        private async void OnPrevVideo()
        {
            if (CurrentPlaylist?.CurrentlyPlayingId != null &&
                CurrentPlaylist?.Videos != null &&
                CurrentPlaylist.CurrentlyPlayingId - 1 >= 0)
            {
                CurrentPlaylist.CurrentlyPlayingId--;
                await _mediaService.Open(CurrentPlaylist.Videos[CurrentPlaylist.CurrentlyPlayingId.Value]);
            }
        }


        private void OnFullscreen()
        {
            Messenger.Default.Send(new ToggleWindowFullscreenMessage());
        }
    }
}
