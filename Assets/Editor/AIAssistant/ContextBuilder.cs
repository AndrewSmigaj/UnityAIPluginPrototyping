using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditor.AIAssistant
{
    /// <summary>
    /// Builds context packs for the OpenAI Responses API.
    /// Assembles project artifacts into a formatted text payload optimized for GPT-5.
    /// Includes tool instructions, project metadata, active scene data, and scripts.
    /// </summary>
    public static class ContextBuilder
    {
        /// <summary>
        /// Builds a complete context pack for the AI from indexed artifacts and user prompt.
        /// </summary>
        /// <param name="userPrompt">The user's natural language request</param>
        /// <param name="tokenBudget">Maximum estimated tokens (approximate, for awareness only)</param>
        /// <returns>Formatted context pack string ready for API</returns>
        public static string BuildContextPack(string userPrompt, int tokenBudget)
        {
            var sb = new StringBuilder();

            // Header: Tool instructions for GPT-5
            sb.AppendLine("You are a Unity scene assistant with the following capabilities:");
            sb.AppendLine();
            sb.AppendLine("Tools Available:");
            sb.AppendLine("- createRectangle: Creates rectangle sprite at world position");
            sb.AppendLine("- createCircle: Creates circle sprite at world position");
            sb.AppendLine();
            sb.AppendLine("Instructions:");
            sb.AppendLine("- All positions are Unity world coordinates");
            sb.AppendLine("- Colors must be hex format (#RRGGBB)");
            sb.AppendLine("- Be helpful and conversational");
            sb.AppendLine("- If uncertain, ask clarifying questions");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine("Context Pack v0.1");
            sb.AppendLine();

            // Section 1: Project Metadata
            sb.AppendLine("## Project Metadata");
            string projectMetadata = ReadArtifact(Path.Combine(ProjectIndexer.PROJECT_ARTIFACTS, "ProjectMetadata.json"));
            sb.AppendLine(projectMetadata);
            sb.AppendLine();

            // Section 2: Active Scene
            // NOTE: Use CURRENT active scene, not cached metadata (user may have switched scenes)
            Scene activeScene = SceneManager.GetActiveScene();
            string activeSceneName = activeScene.name;

            sb.AppendLine("## Active Scene");
            if (!string.IsNullOrEmpty(activeSceneName))
            {
                string sceneArtifact = ReadArtifact(Path.Combine(ProjectIndexer.SCENES_ARTIFACTS, $"{activeSceneName}.json"));
                sb.AppendLine(sceneArtifact);
            }
            else
            {
                sb.AppendLine("{}"); // No active scene
            }
            sb.AppendLine();

            // Section 3: Scripts
            sb.AppendLine("## Scripts");
            string scripts = ReadArtifact(Path.Combine(ProjectIndexer.SCRIPTS_ARTIFACTS, "AllScripts.json"));
            sb.AppendLine(scripts);
            sb.AppendLine();

            // Footer: User request
            sb.AppendLine("---");
            sb.AppendLine($"User Request: {userPrompt}");

            string contextPack = sb.ToString();

            // Token budget awareness (warning only, no truncation for Day 1)
            int estimatedTokens = EstimateTokens(contextPack);
            if (estimatedTokens > tokenBudget)
            {
                Debug.LogWarning($"[AI Assistant] Context pack (~{estimatedTokens} tokens) exceeds budget ({tokenBudget}). " +
                                 "Proceeding anyway - GPT-5 supports up to 272,000 input tokens.");
            }

            return contextPack;
        }

        /// <summary>
        /// Reads an artifact file from disk.
        /// Returns empty JSON object if file doesn't exist or error occurs.
        /// </summary>
        /// <param name="path">Path to artifact file</param>
        /// <returns>File contents or "{}" if missing/error</returns>
        private static string ReadArtifact(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path);
                }
                else
                {
                    Debug.LogWarning($"[AI Assistant] Artifact not found: {path}. Using empty placeholder.");
                    return "{}";
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AI Assistant] Failed to read artifact {path}: {ex.Message}");
                return "{}";
            }
        }

        /// <summary>
        /// Estimates token count for a string.
        /// Uses simple approximation: ~4 characters per token (OpenAI's recommended fallback).
        /// NOTE: Actual token count may vary by ±20%. This is for budget awareness only.
        /// </summary>
        /// <param name="text">Text to estimate</param>
        /// <returns>Estimated token count</returns>
        private static int EstimateTokens(string text)
        {
            // Approximate token count - actual count may vary
            // OpenAI recommends ~4 chars per token as fallback when tiktoken unavailable
            return text.Length / 4;
        }
    }
}
