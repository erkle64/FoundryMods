using C3;
using System.Collections.Generic;
using Unfoundry;
using UnityEngine;

namespace ErkleNuke
{

    [FoundryRPC]
    [AddSystemToGameSimulation]
    public class NukeSystem : SystemManager.System
    {
        private const float SHOCKWAVE_RADIUS = 100f;
        private const float SHOCKWAVE_SPACING = 4f;
        private const int SHOCKWAVE_BLAST_RADIUS = 5;
        private const int SHOCKWAVE_BLASTS_PER_STEP = 32;
        private const float SHOCKWAVE_BLAST_STEPS_PER_SECOND = 5f;

        private static GameObject _explosionPrefab = null;

        public override void OnAddedToWorld()
        {
            _explosionPrefab = AssetManager.Database.LoadAssetAtPath<GameObject>("Assets/erkle64.Nuke/Hovl Studio/Nuclear explosion/Prefabs/Nuclear explosion.prefab");
            if (_explosionPrefab == null)
            {
                Debug.LogWarning("Nuclear Explosion prefab not found!");
                return;
            }
        }

        public static void TriggerNuke(HashSet<Vector3Int> nukePositions)
        {
            var time = Time.time;
            foreach (var nukePosition in nukePositions)
            {
                ActionManager.AddTimedAction(time + 0.1f, () => Rpc.Lockstep.Run(TriggerExplosionEffectRPC, nukePosition + new Vector3(0.5f, 0.5f, 0.5f)));

                var startIndex = CountPointsInSpiral(10.0f, SHOCKWAVE_SPACING);
                var endIndex = CountPointsInSpiral(SHOCKWAVE_RADIUS + SHOCKWAVE_BLAST_RADIUS, SHOCKWAVE_SPACING);

                for (int i = startIndex; i < endIndex; i += SHOCKWAVE_BLASTS_PER_STEP)
                {
                    var position = nukePosition;
                    var index = i;
                    ActionManager.AddTimedAction(time + i * 1.0f / (SHOCKWAVE_BLAST_STEPS_PER_SECOND * SHOCKWAVE_BLASTS_PER_STEP), () => Rpc.Lockstep.Run(ProcessShockwaveRPC, position, index));
                }
            }
        }

        [FoundryRPC]
        public static void TriggerExplosionEffectRPC(Vector3 nukePosition)
        {
            if (_explosionPrefab != null)
            {
                var explosion = Object.Instantiate(_explosionPrefab, nukePosition, Quaternion.identity);
                explosion.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

                ActionManager.AddTimedAction(Time.time + 14.8f, () => Object.Destroy(explosion));
            }
        }

        [FoundryRPC]
        public static void ProcessShockwaveRPC(Vector3Int nukePosition, int index)
        {
            // native start
            BuildingManager.buildingManager_startDynamiteExplosion();

            // init lists for data storage
            var hs_chunksWithinExplosionRadius = new HashSet<ulong>();
            var list_dynamitesToExplode = new List<DynamiteEntity>();

            for (int i = 0; i < SHOCKWAVE_BLASTS_PER_STEP; i++)
            {
                PolarPointOnSpiral(index + i, SHOCKWAVE_SPACING, out float angle, out float radius);
                if (radius > SHOCKWAVE_RADIUS)
                    radius = SHOCKWAVE_RADIUS;

                var dynamitePos = nukePosition + new Vector3(radius * Mathf.Cos(angle), 0.0f, radius * Mathf.Sin(angle));
                var dynamiteBlockPos = Vector3Int.FloorToInt(dynamitePos);

                var explosionRadius = SHOCKWAVE_BLAST_RADIUS;

                // iterate explosion radius per block
                //var shatterCubeSpawnCounter = 0;
                for (int x = -explosionRadius; x <= explosionRadius; x++)
                {
                    var explosionRadiusY = Mathf.CeilToInt(Mathf.Sqrt(explosionRadius * explosionRadius - x * x));

                    for (int y = -explosionRadiusY; y <= explosionRadiusY; y++)
                    {
                        var explosionRadiusZ = Mathf.CeilToInt(Mathf.Sqrt(explosionRadius * explosionRadius - x * x - y * y));

                        for (int z = -explosionRadius; z <= explosionRadius; z++)
                        {
                            // ignore if outside of sphere equation
                            /*if ((x * x + y * y + z * z) >= explosionRadius * explosionRadius)
                                continue;*/

                            // apply iterated offset to get world position and query terrain data
                            Vector3Int worldPos = new Vector3Int(dynamiteBlockPos.x + x, dynamiteBlockPos.y + y, dynamiteBlockPos.z + z);
                            byte terrainByteIdx = ChunkManager.getTerrainDataForWorldCoord(worldPos, out Chunk chunk, out uint blockIdx);
                            if (chunk == null)
                                continue;

                            // TERRAIN EXISTS AT ITERATED POSITION
                            if (terrainByteIdx != 0 && terrainByteIdx < GameRoot.BUILDING_PART_ARRAY_IDX_START)
                            {
                                // get template and try remove block
                                TerrainBlockType tbt = ItemTemplateManager.getTerrainBlockTemplateByByteIdx(terrainByteIdx);
                                byte tbtId = 0; // not needed, but has to be passed

                                // notes: here we use the non-utility-wrapped removal method because we don't need any of the additional features,
                                // no shatter cube, no sfx, not world decor removal, no item sets/inventories filled with the yield.
                                if (tbt.destructible == true &&
                                    tbt.requiredMiningHardnessLevel <= ResearchSystem.getUnlockedMiningHardnessLevel() &&
                                    ChunkManager.chunks_removeTerrainBlock(chunk.chunkId, blockIdx, ref tbtId) == IOBool.iotrue)
                                {
                                    // spawn low shatter cube every 20th cube
                                    /*shatterCubeSpawnCounter++;
                                    if (shatterCubeSpawnCounter >= 20)
                                    {
                                        if (ChunkManager.isChunkVisible(chunk.chunkId) == true)
                                        {
                                            // queue delayed spawn
                                            ChunkManager.queueDelayedVisualObjectSpawn(
                                                new ChunkManager.DelayedVisualObjectSpawn(
                                                    0, ResourceDB.lowShatterCube,
                                                    new Vector3(worldPos.x + 0.5f, worldPos.y + 0.5f, worldPos.z + 0.5f), Quaternion.identity, Vector3.one,
                                                    true, 100f, worldPos, 10f,
                                                    tbt, null));

                                            // reset counter
                                            shatterCubeSpawnCounter = 0;
                                        }
                                    }*/
                                }
                            }
                            // BUILDING PART EXISTS AT ITERATED POSITION
                            else if (terrainByteIdx != 0 && terrainByteIdx >= GameRoot.BUILDING_PART_ARRAY_IDX_START)
                            {
                                // get building part entity id
                                byte bpByteIdx = chunk.getBuildingPartArrayData(blockIdx, out ulong bpEntityId);
                                if (bpByteIdx != 0 && bpEntityId != 0)
                                {
                                    // destroy building part and flag chunk for mesh re-gen
                                    BuildingManager.buildingManager_demolishBuildingEntityForDynamite(bpEntityId);
                                }
                            }

                            // build hashset with unique chunk ids within explosion radius
                            hs_chunksWithinExplosionRadius.Add(chunk.chunkId);
                        }
                    }

                    // iterate all unique chunks insides the current dynamites explosion radius
                    foreach (ulong cidx in hs_chunksWithinExplosionRadius)
                    {
                        // check all of chunk's dynamites and check if they are near enough to current dynamite so that it would trigger a chain reaction.
                        var chunkItr = ChunkManager.getChunkByIdx(cidx);
                        foreach (var dynamiteEntityItr in chunkItr.dict_dynamiteEntities.Values)
                        {
                            // ignore if already in "to-check" list
                            if (dynamiteEntityItr._addedToIterationList == true)
                                continue;

                            // add to list with exploding dynamites if in range to trigger chain-reaction
                            if (Vector3Int.Distance(dynamiteBlockPos, dynamiteEntityItr.positionAfterLanding_fpm.toBlockCoordinates()) <= SHOCKWAVE_BLAST_RADIUS)
                            {
                                // add to processing list                                    
                                list_dynamitesToExplode.Add(dynamiteEntityItr);
                                dynamiteEntityItr._addedToIterationList = true;
                            }
                        }
                    }
                    hs_chunksWithinExplosionRadius.Clear();

                    // call native building handling
                    AABB3D aabbExplosionArea = new AABB3D(0, 0, 0, 0, 0, 0); // only used for aabb based explosion type
                    BuildingManager.buildingManager_processDynamiteExplosion(new v3i(dynamiteBlockPos), explosionRadius, 1, new v3(dynamitePos), aabbExplosionArea);
                }
            }

            // finalize (spawns debris)
            BuildingManager.buildingManager_finalizeDynamiteExplosion();
        }

        private static void PolarPointOnSpiral(float t, float spacing, out float angle, out float radius)
        {
            angle = Mathf.Sqrt(t) * 3.542f;
            radius = angle * spacing / (Mathf.PI * 2.0f);
        }

        private static int CountPointsInSpiral(float radius, float spacing)
        {
            var scaledRadius = radius / spacing;
            return Mathf.CeilToInt(scaledRadius * scaledRadius * 3.146755f);
        }
    }

}