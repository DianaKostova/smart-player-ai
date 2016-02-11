﻿using SmartPlayer.Core.Repositories;
using SmartPlayer.Core.SongAnalyzer;
using SmartPlayer.Data;
using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Configuration.Assemblies;
using System.Configuration;
using System.IO;
using SmartPlayer.Core.DTOs;
using SmartPlayer.Core.Utility;

namespace SmartPlayer.Core.BusinessServices
{
    public class MusicService
    {
        public void Store(string originalFileName, string guid)
        {
            string mediaServerUrlBase = ConfigurationManager.AppSettings["MediaServerSaveBaseUrl"];
            string fullName = Path.Combine(mediaServerUrlBase, guid);

            double songGrade = Analyzer.GetGradeFor(fullName);

            Song song = new Song()
            {
                Name = originalFileName,
                Guid = guid,
                Grade = songGrade
            };

            SmartPlayerEntities context = new SmartPlayerEntities();
            MusicRepository repo = new MusicRepository(context);

            repo.Create(song);

            context.Dispose(); // TODO find a better way to handle dbcontext
        }

        public List<SongDto> GetAllSongs()
        {
            SmartPlayerEntities context = new SmartPlayerEntities();

            MusicRepository repo = new MusicRepository(context);

            var allSongs = repo.GetAll()
                .Select(x => new SongDto
                {
                    Id = x.Id,
                    SongName = x.Name
                })
                .ToList();

            context.Dispose();

            return allSongs;
        }

        public PlayerSongDto GetNextSong(NextSongDto songRequest, string username = null)
        {
            using (SmartPlayerEntities context = new SmartPlayerEntities())
            {
                MusicRepository musicRepo = new MusicRepository(context);
                var currentSong = musicRepo.GetSongById(songRequest.CurrentSongId);
                var excludedSongIdList = songRequest.PlayedSongIds;

                var recommendedSongs = GetRecommendedSongsForUser(username, context);
                recommendedSongs = recommendedSongs.Where(x => !excludedSongIdList.Contains(x.Id)).ToList();

                var similarSongs = musicRepo.GetNextSongBasedOnUserAndGrade(currentSong.Grade);
                similarSongs = similarSongs.Where(x => !excludedSongIdList.Contains(x.Id)).ToList();

                var bestSongsForUser = new List<Song>();
                if(recommendedSongs.Any() && similarSongs.Any())
                {
                    bestSongsForUser = recommendedSongs.Intersect(similarSongs).ToList();
                    if(!bestSongsForUser.Any())
                    {
                        bestSongsForUser = recommendedSongs.Union(similarSongs).ToList();
                    }
                }
                else
                {
                    bestSongsForUser = recommendedSongs.Union(similarSongs).ToList();
                }

                var selectedSong = bestSongsForUser.First();

                var songUrl = string.Format("http://{0}{1}{2}",
                    IpV4Provider.GetLocalIPAddress(),
                    ConfigurationManager.AppSettings["MediaServerLoadPort"],
                    selectedSong.Guid);

                PlayerSongDto song = new PlayerSongDto()
                {
                    Id = selectedSong.Id,
                    Name = selectedSong.Name,
                    Url =  songUrl,
                    CurrentUserVote = GetUserRatingForSong(context, username, selectedSong)
                };

                return song;
            }
        }

        private static List<Song> GetRecommendedSongsForUser(string username, SmartPlayerEntities context)
        {
            var recommendedSongs = new List<Song>();
            if (username != null)
            {
                UserRepository userRepo = new UserRepository(context);
                var userId = userRepo.GetUserByUsername(username).Id;

                PearsonScoreCalculator calculator = new PearsonScoreCalculator(context);
                recommendedSongs = calculator.GetBestSongsForUser(userId);
            }
            return recommendedSongs;
        }

        public PlayerSongDto GetSong(int songId, string username)
        {
            SmartPlayerEntities context = new SmartPlayerEntities();
            MusicRepository repo = new MusicRepository(context);

            var requestedSong = repo.GetSongById(songId);

            var songUrl = string.Format("http://{0}{1}{2}",
                IpV4Provider.GetLocalIPAddress(),
                ConfigurationManager.AppSettings["MediaServerLoadPort"],
                requestedSong.Guid);

            PlayerSongDto song = new PlayerSongDto()
            {
                Id = requestedSong.Id,
                Name = requestedSong.Name,
                Url = songUrl,
                CurrentUserVote = GetUserRatingForSong(context, username, requestedSong)
            };

            context.Dispose();

            return song;
        }

        public List<SongDto> SearchSong(string query)
        {
            SmartPlayerEntities context = new SmartPlayerEntities();
            MusicRepository repo = new MusicRepository(context);

            var requestedSongs = repo.SearchByTerm(query);

            context.Dispose();

            return requestedSongs.Select(x => new SongDto() { Id = x.Id, SongName = x.Name }).ToList();
        }

        public void RateSong(SongRatingDto rating, string userName)
        {
            SmartPlayerEntities context = new SmartPlayerEntities();

            UserRepository userRepo = new UserRepository(context);

            User currentUser = userRepo.GetAll().First(x => x.Email == userName);

            context.UserSongVotes.Add(new UserSongVote()
                {
                    Rating = rating.Rating,
                    SongId = rating.SongId,
                    UserId = currentUser.Id
                });
            context.SaveChanges();
        }

        private int? GetUserRatingForSong(SmartPlayerEntities context, string username, Song song)
        {
            var currentUser = default(User);
            if (!string.IsNullOrWhiteSpace(username))
            {
                var userRepo = new UserRepository(context);
                currentUser = userRepo.GetUserByUsername(username);
            }

            var songVote = default(int?);
            if (currentUser != null && song.UserSongVotes.Where(x => x.UserId == currentUser.Id).Any())
            {
                songVote = song.UserSongVotes.First(x => x.UserId == currentUser.Id).Rating;
            }

            return songVote;
        }
    }
}
