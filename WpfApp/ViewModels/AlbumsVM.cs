﻿using ReactiveUI;
using ReactiveUI.Validation.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using WpfApp.Model;
using System.Linq;
using DynamicData;

namespace WpfApp.ViewModels
{
    public sealed class AlbumsVM : ReactiveValidationObject, IDisposable
    {
        private readonly CompositeDisposable _disposables;
        private ReadOnlyObservableCollection<AlbumVM>? _albums;
        private string? _albumSearch;

        public AlbumsVM(IObservable<User?> user)
        {
            _disposables = new CompositeDisposable();

            user.WhereNotNull()
                .Subscribe(async user =>
            {
                var albums = new SourceCache<Album, int>(a => a.Id);
                albums.AddOrUpdate(await user.GetAlbums());

                var albumSearchFilter = this.WhenAnyValue(x => x.AlbumSearch)
                    .Throttle(TimeSpan.FromSeconds(0.5), RxApp.TaskpoolScheduler)
                    .Select(query => query?.Trim())
                    .DistinctUntilChanged()
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Select(MakeAlbumFilter);

                var albumsCache = albums
                    .Connect()
                    .Filter(albumSearchFilter)
                    .AsObservableCache();

                albumsCache.DisposeWith(_disposables);

                albumsCache
                    .Connect()
                    .Transform(x => new AlbumVM(x))
                    .ObserveOn(RxApp.MainThreadScheduler)
                    .Bind(out _albums)
                    .Subscribe(a => this.RaisePropertyChanged(nameof(Albums)))
                    .DisposeWith(_disposables);
            }).DisposeWith(_disposables);
        }

        public ReadOnlyObservableCollection<AlbumVM>? Albums => _albums;

        public string? AlbumSearch
        {
            get => _albumSearch;
            set => this.RaiseAndSetIfChanged(ref _albumSearch, value);
        }

        private static Func<Album, bool> MakeAlbumFilter(string? filter)
        {
            return album => string.IsNullOrWhiteSpace(filter) || album.Title.Contains(filter, StringComparison.InvariantCultureIgnoreCase);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
