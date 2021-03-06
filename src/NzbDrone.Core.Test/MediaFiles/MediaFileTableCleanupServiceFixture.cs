using System.Collections.Generic;
using System.Linq;
using System.IO;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    public class MediaFileTableCleanupServiceFixture : CoreTest<MediaFileTableCleanupService>
    {
        private const string DELETED_PATH = "ANY FILE WITH THIS PATH IS CONSIDERED DELETED!";
        private List<Track> _tracks;
        private Artist _artist;

        [SetUp]
        public void SetUp()
        {
            _tracks = Builder<Track>.CreateListOfSize(10)
                  .Build()
                  .ToList();

            _artist = Builder<Artist>.CreateNew()
                                     .With(s => s.Path = @"C:\Test\Music\Artist".AsOsAgnostic())
                                     .Build();

            Mocker.GetMock<IDiskProvider>()
                  .Setup(e => e.FileExists(It.Is<string>(c => !c.Contains(DELETED_PATH))))
                  .Returns(true);

            Mocker.GetMock<ITrackService>()
                  .Setup(c => c.GetTracksByArtist(It.IsAny<int>()))
                  .Returns(_tracks);
        }

        private void GivenTrackFiles(IEnumerable<TrackFile> trackFiles)
        {
            Mocker.GetMock<IMediaFileService>()
                  .Setup(c => c.GetFilesByArtist(It.IsAny<int>()))
                  .Returns(trackFiles.ToList());
        }

        private void GivenFilesAreNotAttachedToTrack()
        {
            _tracks.ForEach(e => e.TrackFileId = 0);

            Mocker.GetMock<ITrackService>()
                  .Setup(c => c.GetTracksByArtist(It.IsAny<int>()))
                  .Returns(_tracks);
        }

        private List<string> FilesOnDisk(IEnumerable<TrackFile> trackFiles)
        {
            return trackFiles.Select(e => Path.Combine(_artist.Path, e.RelativePath)).ToList();
        }

        [Test]
        public void should_skip_files_that_exist_in_disk()
        {
            var trackFiles = Builder<TrackFile>.CreateListOfSize(10)
                .Build();

            GivenTrackFiles(trackFiles);

            Subject.Clean(_artist, FilesOnDisk(trackFiles));

            Mocker.GetMock<ITrackService>().Verify(c => c.UpdateTrack(It.IsAny<Track>()), Times.Never());
        }

        [Test]
        public void should_delete_non_existent_files()
        {
            var trackFiles = Builder<TrackFile>.CreateListOfSize(10)
                .Random(2)
                .With(c => c.RelativePath = DELETED_PATH)
                .Build();

            GivenTrackFiles(trackFiles);

            Subject.Clean(_artist, FilesOnDisk(trackFiles.Where(e => e.RelativePath != DELETED_PATH)));

            Mocker.GetMock<IMediaFileService>().Verify(c => c.Delete(It.Is<TrackFile>(e => e.RelativePath == DELETED_PATH), DeleteMediaFileReason.MissingFromDisk), Times.Exactly(2));
        }

        [Test]
        public void should_delete_files_that_dont_belong_to_any_tracks()
        {
            var trackFiles = Builder<TrackFile>.CreateListOfSize(10)
                                .Random(10)
                                .With(c => c.RelativePath = "ExistingPath")
                                .Build();

            GivenTrackFiles(trackFiles);
            GivenFilesAreNotAttachedToTrack();

            Subject.Clean(_artist, FilesOnDisk(trackFiles));

            Mocker.GetMock<IMediaFileService>().Verify(c => c.Delete(It.IsAny<TrackFile>(), DeleteMediaFileReason.NoLinkedEpisodes), Times.Exactly(10));
        }

        [Test]
        public void should_unlink_track_when_trackFile_does_not_exist()
        {
            GivenTrackFiles(new List<TrackFile>());

            Subject.Clean(_artist, new List<string>());

            Mocker.GetMock<ITrackService>().Verify(c => c.UpdateTrack(It.Is<Track>(e => e.TrackFileId == 0)), Times.Exactly(10));
        }

        [Test]
        public void should_not_update_track_when_trackFile_exists()
        {
            var trackFiles = Builder<TrackFile>.CreateListOfSize(10)
                                .Random(10)
                                .With(c => c.RelativePath = "ExistingPath")
                                .Build();

            GivenTrackFiles(trackFiles);

            Subject.Clean(_artist, FilesOnDisk(trackFiles));

            Mocker.GetMock<ITrackService>().Verify(c => c.UpdateTrack(It.IsAny<Track>()), Times.Never());
        }
    }
}
