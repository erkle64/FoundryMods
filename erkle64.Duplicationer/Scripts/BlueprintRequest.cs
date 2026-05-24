using UnityEngine;

namespace Duplicationer
{
    public class BlueprintRequest
    {
        public int positionX;
        public int positionY;
        public int positionZ;
        public int sizeX;
        public int sizeY;
        public int sizeZ;
        public Building[] buildings;
        public TrainTrack[] tracks;
        public byte[] blocks;

        public struct Building
        {
            public ulong templateId;
            public int anchorPositionX;
            public int anchorPositionY;
            public int anchorPositionZ;
            public BuildingManager.BuildOrientation orientationY;
            public float orientationUnlockedX;
            public float orientationUnlockedY;
            public float orientationUnlockedZ;
            public float orientationUnlockedW;
            public byte itemMode;
            public (string, string)[] customData;
        }

        public struct TrainTrack
        {
            public ulong templateId;
            public int anchorPositionX;
            public int anchorPositionY;
            public int anchorPositionZ;
            public int orientationY;
        }
    }
}