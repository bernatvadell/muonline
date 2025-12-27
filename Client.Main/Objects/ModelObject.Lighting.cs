using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Client.Main.Objects
{
    public abstract partial class ModelObject
    {
        private void PrepareDynamicLightingEffect(Effect effect)
        {
            effect.CurrentTechnique = effect.Techniques["DynamicLighting"];
            GraphicsManager.Instance.ShadowMapRenderer?.ApplyShadowParameters(effect);

            var camera = Camera.Instance;
            if (camera == null)
                return;

            // Set transformation matrices
            effect.Parameters["World"]?.SetValue(WorldPosition);
            effect.Parameters["View"]?.SetValue(camera.View);
            effect.Parameters["Projection"]?.SetValue(camera.Projection);
            Matrix worldViewProjection = WorldPosition * camera.View * camera.Projection;
            effect.Parameters["WorldViewProjection"]?.SetValue(worldViewProjection);
            effect.Parameters["EyePosition"]?.SetValue(camera.Position);

            Vector3 sunDir = GraphicsManager.Instance.ShadowMapRenderer?.LightDirection ?? Constants.SUN_DIRECTION;
            if (sunDir.LengthSquared() < 0.0001f)
                sunDir = new Vector3(1f, 0f, -0.6f);
            sunDir = Vector3.Normalize(sunDir);
            bool worldAllowsSun = World is WorldControl wc ? wc.IsSunWorld : true;
            bool sunEnabled = Constants.SUN_ENABLED && worldAllowsSun && UseSunLight && !HasWalkerAncestor();
            effect.Parameters["SunDirection"]?.SetValue(sunDir);
            effect.Parameters["SunColor"]?.SetValue(_sunColor);
            effect.Parameters["SunStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveSunStrength() : 0f);
            effect.Parameters["ShadowStrength"]?.SetValue(sunEnabled ? SunCycleManager.GetEffectiveShadowStrength() : 0f);

            effect.Parameters["Alpha"]?.SetValue(TotalAlpha);
            // Use objects technique instead of setting uniforms (better performance, no shader branches)
            effect.CurrentTechnique = effect.Techniques["DynamicLighting"];
            effect.Parameters["TerrainDynamicIntensityScale"]?.SetValue(1.5f);
            effect.Parameters["AmbientLight"]?.SetValue(_ambientLightVector * SunCycleManager.AmbientMultiplier);
            effect.Parameters["DebugLightingAreas"]?.SetValue(Constants.DEBUG_LIGHTING_AREAS ? 1.0f : 0.0f);

            // Set terrain lighting (cached per draw pass)
            Vector3 worldTranslation = WorldPosition.Translation;
            Vector3 terrainLight = Vector3.One;
            if (LightEnabled && World?.Terrain != null)
                terrainLight = World.Terrain.EvaluateTerrainLight(worldTranslation.X, worldTranslation.Y);
            terrainLight = Vector3.Clamp(terrainLight / 255f, Vector3.Zero, Vector3.One);
            effect.Parameters["TerrainLight"]?.SetValue(terrainLight);

            // Select dynamic lights (cached per object, updated on throttled light version changes)
            if (!Constants.ENABLE_DYNAMIC_LIGHTS)
            {
                effect.Parameters["ActiveLightCount"]?.SetValue(0);
                effect.Parameters["MaxLightsToProcess"]?.SetValue(0);
                return;
            }

            int maxLights = Constants.OPTIMIZE_FOR_INTEGRATED_GPU ? 4 : 16;
            int lightCount = 0;
            var terrain = World?.Terrain;
            if (terrain != null)
            {
                int version = terrain.ActiveLightsVersion;
                var activeLights = terrain.ActiveLights;

                if (_dynamicLightSelectionVersion != version || _dynamicLightSelectionMaxLights != maxLights)
                {
                    _dynamicLightSelectionCount = 0;
                    if (activeLights != null && activeLights.Count > 0)
                    {
                        EnsureDynamicLightSelectionBuffer();
                        _dynamicLightSelectionCount = SelectRelevantLightIndices(activeLights, worldTranslation, maxLights, _dynamicLightSelectionIndices);
                    }

                    _dynamicLightSelectionVersion = version;
                    _dynamicLightSelectionMaxLights = maxLights;
                }

                if (activeLights != null && _dynamicLightSelectionCount > 0)
                {
                    lightCount = FillSelectedLightArrays(activeLights, _dynamicLightSelectionIndices, _dynamicLightSelectionCount);
                }
            }

            effect.Parameters["ActiveLightCount"]?.SetValue(lightCount);
            effect.Parameters["MaxLightsToProcess"]?.SetValue(maxLights);
            if (lightCount > 0)
            {
                effect.Parameters["LightPositions"]?.SetValue(_cachedLightPositions);
                effect.Parameters["LightColors"]?.SetValue(_cachedLightColors);
                effect.Parameters["LightRadii"]?.SetValue(_cachedLightRadii);
                effect.Parameters["LightIntensities"]?.SetValue(_cachedLightIntensities);
            }
        }

        private void EnsureDynamicLightSelectionBuffer()
        {
            if (_dynamicLightSelectionIndices == null || _dynamicLightSelectionIndices.Length != _cachedLightPositions.Length)
            {
                _dynamicLightSelectionIndices = new int[_cachedLightPositions.Length];
            }
        }

        private int FillSelectedLightArrays(IReadOnlyList<DynamicLightSnapshot> activeLights, int[] selectedIndices, int count)
        {
            int filled = 0;
            int max = Math.Min(count, _cachedLightPositions.Length);
            for (int i = 0; i < max; i++)
            {
                int idx = selectedIndices[i];
                if ((uint)idx >= (uint)activeLights.Count)
                    continue;

                var light = activeLights[idx];
                _cachedLightPositions[filled] = light.Position;
                _cachedLightColors[filled] = light.Color;
                _cachedLightRadii[filled] = light.Radius;
                _cachedLightIntensities[filled] = light.Intensity;
                filled++;
            }
            return filled;
        }

        private int SelectRelevantLightIndices(IReadOnlyList<DynamicLightSnapshot> activeLights, Vector3 worldTranslation, int maxLights, int[] selectedIndices)
        {
            maxLights = Math.Min(maxLights, _cachedLightPositions.Length);
            maxLights = Math.Min(maxLights, selectedIndices.Length);
            if (activeLights == null || activeLights.Count == 0 || maxLights <= 0)
                return 0;

            int selected = 0;
            float weakestScore = float.MaxValue;
            int weakestIndex = 0;
            var obj2D = new Vector2(worldTranslation.X, worldTranslation.Y);

            for (int i = 0; i < activeLights.Count; i++)
            {
                var light = activeLights[i];
                float radius = light.Radius;
                float radiusSq = radius * radius;

                var diff = new Vector2(light.Position.X, light.Position.Y) - obj2D;
                float distSq = diff.LengthSquared();
                if (distSq >= radiusSq)
                    continue;

                float influence = (1f - distSq / radiusSq) * light.Intensity;
                if (influence <= MinLightInfluence)
                    continue;

                if (selected < maxLights)
                {
                    _cachedLightScores[selected] = influence;
                    selectedIndices[selected] = i;

                    if (influence < weakestScore)
                    {
                        weakestScore = influence;
                        weakestIndex = selected;
                    }

                    selected++;
                }
                else if (influence > weakestScore)
                {
                    _cachedLightScores[weakestIndex] = influence;
                    selectedIndices[weakestIndex] = i;

                    weakestScore = _cachedLightScores[0];
                    weakestIndex = 0;
                    for (int j = 1; j < selected; j++)
                    {
                        float score = _cachedLightScores[j];
                        if (score < weakestScore)
                        {
                            weakestScore = score;
                            weakestIndex = j;
                        }
                    }
                }
            }

            return selected;
        }
    }
}
