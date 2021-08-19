using System;
using UnityEngine;
using VRC.Udon;

namespace VRC.Examples.Obstacle
{
    public class ObstacleCourseAsset : ScriptableObject
    {
        public GameObject[] checkpointPrefabs;
        public GameObject[] powerUpPrefabs;
        public GameObject playerObjectPrefab;
        public int playerCount = 8;
        public GameObject scoreObjectPrefab;
        public int scoreFieldsCount = 5;
        public float moveSpeed = 6;
        public float jumpImpulse = 5;
        public LayerMask creationMask;
        public bool showAdvanced = false;
        public float recordPathYOffset = 0.5f;
        public bool autoUpdateCheckpoints = true;
        public bool showCheckpointLabels = true;
        public float checkpointLabelOffset = 5;

        // Todo: set default programs?
        public AbstractUdonProgramSource courseProgram;
        public AbstractUdonProgramSource scoreManagerProgram;
        public AbstractUdonProgramSource playerModsManagerProgram;
        public AbstractUdonProgramSource playerDataManagerProgram;
        public AbstractUdonProgramSource checkpointProgram;
        public AbstractUdonProgramSource powerUpProgram;

        public StringStringDictionary variableToSceneObjectLookup;
    }
    
    [Serializable]
    public class StringStringDictionary : SerializableDictionary<string, string> { }
}