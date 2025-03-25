using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Standalone script to enforce a global player limit when joining lobbies via QuickJoin.
    /// Works with XRINetworkGameManager without modifying its code.
    /// </summary>
    public class PlayerLimitController : MonoBehaviour
    {
        /// <summary>
        /// Maximum total number of players allowed across all lobbies.
        /// </summary>
        [SerializeField, Tooltip("The maximum total number of players allowed across all lobbies.")]
        private int maxTotalPlayers = 1;

        /// <summary>
        /// Reference to the XRINetworkGameManager instance in the scene.
        /// </summary>
        [SerializeField, Tooltip("The XRINetworkGameManager instance to interact with.")]
        private XRINetworkGameManager networkGameManager;

        private const string k_DebugPrepend = "<color=#00CED1>[Player Limit Controller]</color> ";

        /// <summary>
        /// See <see cref="MonoBehaviour"/>.
        /// </summary>
        private void Awake()
        {
            // Find XRINetworkGameManager if not assigned in the Inspector
            if (networkGameManager == null)
            {
                networkGameManager = FindFirstObjectByType<XRINetworkGameManager>();
                if (networkGameManager == null)
                {
                    Utils.Log($"{k_DebugPrepend}No XRINetworkGameManager found in scene. Disabling PlayerLimitController.", 2);
                    enabled = false;
                    return;
                }
            }
            Utils.Log($"{k_DebugPrepend}Initialized with XRINetworkGameManager.");
        }

        /// <summary>
        /// Checks if joining a lobby would exceed the global player limit.
        /// </summary>
        /// <returns>True if joining is allowed, false if the limit would be exceeded.</returns>
        private async Task<bool> CanJoinLobby()
        {
            try
            {
                // Query all existing lobbies
                QueryLobbiesOptions queryOptions = new QueryLobbiesOptions
                {
                    Count = 100, // Adjust based on expected lobby count
                };
                var lobbyResponse = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
                var lobbies = lobbyResponse.Results;

                // Calculate total current players across all lobbies
                int totalPlayers = 0;
                foreach (var lobby in lobbies)
                {
                    totalPlayers += lobby.Players.Count;
                }

                // Check if adding a new player exceeds the limit
                if (totalPlayers >= maxTotalPlayers)
                {
                    Utils.Log($"{k_DebugPrepend}Total player limit ({maxTotalPlayers}) reached. Cannot join lobby.", 1);
                    networkGameManager.ConnectionFailed($"Cannot join: Total player limit of {maxTotalPlayers} reached.");
                    return false;
                }

                Utils.Log($"{k_DebugPrepend}Current total players: {totalPlayers}/{maxTotalPlayers}. Join allowed.");
                return true;
            }
            catch (LobbyServiceException ex)
            {
                Utils.Log($"{k_DebugPrepend}Error checking player limit: {ex.Message}", 2);
                networkGameManager.ConnectionFailed("Failed to verify player limit.");
                return false;
            }
        }

        /// <summary>
        /// Attempts to join a lobby via QuickJoin, respecting the global player limit.
        /// Use this instead of XRINetworkGameManager.QuickJoinLobby() to enforce the limit.
        /// </summary>
        public async void QuickJoinWithPlayerLimit()
        {
            Utils.Log($"{k_DebugPrepend}Attempting Quick Join with player limit check.");
            if (await CanJoinLobby())
            {
                // Call the original QuickJoinLobby from XRINetworkGameManager
                networkGameManager.QuickJoinLobby();
            }
            else
            {
                Utils.Log($"{k_DebugPrepend}Quick Join blocked due to player limit.");
            }
        }
    }
}