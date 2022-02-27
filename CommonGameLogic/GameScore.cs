using System.Collections.Generic;

namespace CommonGameLogic
{
    public class GameScore
    {
        private const int SCORE_GOAL = 100;

        private readonly Dictionary<int, int> _scoreById = new Dictionary<int, int>();
        private readonly Dictionary<int, InternalPlayer> _playersById = new Dictionary<int, InternalPlayer>();

        public delegate void ScoreChanged(int player1Score, int player2Score);
        public static event ScoreChanged OnScoreChanged;

        public delegate void PlayerWinEvent(int winnerId);
        public static event PlayerWinEvent OnPlayerWin;

        public static int ScoreGoal()
        {
            return SCORE_GOAL;
        }

        public void AddPlayer(InternalPlayer player)
        {
            _scoreById.Add(player.id, 0);
            _playersById.Add(player.id, player);
        }

        public void AddScoreBonus(int playerId, int bonus)
        {
            _scoreById[playerId] += bonus;

            InvokeOnScoreChangedEvent();
            CheckScoreGoalAndInvokeOnPlayerWinEvent();
        }

        public void ForceUpdateGameScore(Dictionary<int, int> score)
        {
            foreach (var playerScore in score)
            {
                _scoreById[playerScore.Key] = playerScore.Value;
            }

            InvokeOnScoreChangedEvent();
            CheckScoreGoalAndInvokeOnPlayerWinEvent();
        }

        public Dictionary<int, int> GetScore()
        {
            return _scoreById;
        }

        private void InvokeOnScoreChangedEvent()
        {
            if (OnScoreChanged is null)
            {
                return;
            }

            int player1Score = 0;
            int player2Score = 0;

            foreach (var item in _playersById)
            {
                if (item.Value.isLocal)
                {
                    player1Score = _scoreById[item.Key];
                }
                else
                {
                    player2Score = _scoreById[item.Key];
                }
            }

            OnScoreChanged?.Invoke(player1Score, player2Score);
        }

        private void CheckScoreGoalAndInvokeOnPlayerWinEvent()
        {
            if (OnPlayerWin is null)
            {
                return;
            }

            foreach (var playerScore in _scoreById)
            {
                if (playerScore.Value >= SCORE_GOAL)
                {
                    OnPlayerWin?.Invoke(playerScore.Key);
                    break;
                }
            }
        }
    }
}