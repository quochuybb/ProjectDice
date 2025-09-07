using System.Collections.Generic;
using UnityEngine;

public class CombatSpawner : MonoBehaviour
{
    [Tooltip("A list of enemy prefabs to be spawned.")]
    [SerializeField] private List<GameObject> enemyPrefabs;
    
    [Tooltip("A list of empty Transform objects where enemies will be placed.")]
    [SerializeField] private List<Transform> spawnPoints;

    public List<Combatant> SpawnEnemies()
    {
        List<Combatant> spawnedEnemies = new List<Combatant>();
        
        int enemiesToSpawn = Mathf.Min(enemyPrefabs.Count, spawnPoints.Count);

        for (int i = 0; i < enemiesToSpawn; i++)
        {
            GameObject enemyGO = Instantiate(enemyPrefabs[i], spawnPoints[i].position, Quaternion.identity);
            Combatant enemyCombatant = enemyGO.GetComponent<Combatant>();
            if (enemyCombatant != null)
            {
                spawnedEnemies.Add(enemyCombatant);
            }
        }
        return spawnedEnemies;
    }
}