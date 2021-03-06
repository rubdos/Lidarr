using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Profiles.Languages;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class QueueSpecificationFixture : CoreTest<QueueSpecification>
    {
        private Artist _artist;
        private Album _album;
        private RemoteAlbum _remoteAlbum;

        private Artist _otherArtist;
        private Album _otherAlbum;

        [SetUp]
        public void Setup()
        {
            Mocker.Resolve<UpgradableSpecification>();

            _artist = Builder<Artist>.CreateNew()
                                     .With(e => e.Profile = new Profile
                                     {
                                         Items = Qualities.QualityFixture.GetDefaultQualities(),
                                     })
                                     .With(l => l.LanguageProfile = new LanguageProfile
                                     {
                                         Languages = Languages.LanguageFixture.GetDefaultLanguages(),
                                         Cutoff = Language.Spanish
                                     })
                                     .Build();

            _album = Builder<Album>.CreateNew()
                                       .With(e => e.ArtistId = _artist.Id)
                                       .Build();

            _otherArtist = Builder<Artist>.CreateNew()
                                          .With(s => s.Id = 2)
                                          .Build();

            _otherAlbum = Builder<Album>.CreateNew()
                                            .With(e => e.ArtistId = _otherArtist.Id)
                                            .With(e => e.Id = 2)
                                            .Build();

            _remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                   .With(r => r.Artist = _artist)
                                                   .With(r => r.Albums = new List<Album> { _album })
                                                   .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo { Quality = new QualityModel(Quality.MP3_256), Language = Language.Spanish })
                                                   .Build();
        }

        private void GivenEmptyQueue()
        {
            Mocker.GetMock<IQueueService>()
                .Setup(s => s.GetQueue())
                .Returns(new List<Queue.Queue>());
        }

        private void GivenQueue(IEnumerable<RemoteAlbum> remoteAlbums)
        {
            var queue = remoteAlbums.Select(remoteAlbum => new Queue.Queue
            {
                RemoteAlbum = remoteAlbum
            });

            Mocker.GetMock<IQueueService>()
                .Setup(s => s.GetQueue())
                .Returns(queue.ToList());
        }

        [Test]
        public void should_return_true_when_queue_is_empty()
        {
            GivenEmptyQueue();
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_artist_doesnt_match()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                       .With(r => r.Artist = _otherArtist)
                                                       .With(r => r.Albums = new List<Album> { _album })
                                                       .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_quality_in_queue_is_lower()
        {
            _artist.Profile.Value.Cutoff = Quality.MP3_320.Id;
            _artist.LanguageProfile.Value.Cutoff = Language.Spanish;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192),
                                                          Language = Language.Spanish
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_quality_in_queue_is_lower_but_language_is_higher()
        {
            _artist.Profile.Value.Cutoff = Quality.FLAC.Id;
            _artist.LanguageProfile.Value.Cutoff = Language.Spanish;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192),
                                                          Language = Language.English
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_true_when_album_doesnt_match()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _otherAlbum })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192)
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_when_qualities_are_the_same_and_languages_are_the_same()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192),
                                                          Language = Language.Spanish
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_true_when_qualities_are_the_same_but_language_is_better()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_192),
                                                          Language = Language.English,
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_when_quality_in_queue_is_better()
        {
            _artist.Profile.Value.Cutoff = Quality.FLAC.Id;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320),
                                                          Language = Language.English
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_matching_multi_album_is_in_queue()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album, _otherAlbum })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320),
                                                          Language = Language.English
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_multi_album_has_one_album_in_queue()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320),
                                                          Language = Language.English
                                                      })
                                                      .Build();

            _remoteAlbum.Albums.Add(_otherAlbum);

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_multi_part_album_is_already_in_queue()
        {
            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album, _otherAlbum })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_320),
                                                          Language = Language.English
                                                      })
                                                      .Build();

            _remoteAlbum.Albums.Add(_otherAlbum);

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_multi_part_album_has_two_albums_in_queue()
        {
            var remoteAlbums = Builder<RemoteAlbum>.CreateListOfSize(2)
                                                       .All()
                                                       .With(r => r.Artist = _artist)
                                                       .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                       {
                                                           Quality = new QualityModel(Quality.MP3_320),
                                                           Language = Language.English
                                                       })
                                                       .TheFirst(1)
                                                       .With(r => r.Albums = new List<Album> { _album })
                                                       .TheNext(1)
                                                       .With(r => r.Albums = new List<Album> { _otherAlbum })
                                                       .Build();

            _remoteAlbum.Albums.Add(_otherAlbum);
            GivenQueue(remoteAlbums);
            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_quality_and_language_in_queue_meets_cutoff()
        {
            _artist.Profile.Value.Cutoff = _remoteAlbum.ParsedAlbumInfo.Quality.Quality.Id;

            var remoteAlbum = Builder<RemoteAlbum>.CreateNew()
                                                      .With(r => r.Artist = _artist)
                                                      .With(r => r.Albums = new List<Album> { _album })
                                                      .With(r => r.ParsedAlbumInfo = new ParsedAlbumInfo
                                                      {
                                                          Quality = new QualityModel(Quality.MP3_256),
                                                          Language = Language.Spanish
                                                      })
                                                      .Build();

            GivenQueue(new List<RemoteAlbum> { remoteAlbum });

            Subject.IsSatisfiedBy(_remoteAlbum, null).Accepted.Should().BeFalse();
        }
    }
}
