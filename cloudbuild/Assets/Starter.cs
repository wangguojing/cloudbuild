using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Starter : MonoBehaviour {

	// Use this for initialization
	void Start () {
        DontDestroyOnLoad(this);
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    private void OnGUI()
    {
        if (GUI.Button(new Rect(0, 0, 200, 40), "1_multiple_agent_sizes"))
        {
            SceneManager.LoadScene("1_multiple_agent_sizes");
        }

        if (GUI.Button(new Rect(0, 60, 200, 40), "2_drop_plank"))
        {
            SceneManager.LoadScene("2_drop_plank");
        }

        if (GUI.Button(new Rect(0, 120, 200, 40), "3_free_orientation"))
        {
            SceneManager.LoadScene("3_free_orientation");
        }

        if (GUI.Button(new Rect(0, 180, 200, 40), "4_sliding_window_infinite"))
        {
            SceneManager.LoadScene("4_sliding_window_infinite");
        }

        if (GUI.Button(new Rect(0, 240, 200, 40), "5_sliding_window_terrain"))
        {
            SceneManager.LoadScene("5_sliding_window_terrain");
        }

        if (GUI.Button(new Rect(0, 300, 200, 40), "6_modify_mesh"))
        {
            SceneManager.LoadScene("6_modify_mesh");
        }

        if (GUI.Button(new Rect(0, 360, 200, 40), "7_dungeon"))
        {
            SceneManager.LoadScene("7_dungeon");
        }

        if (GUI.Button(new Rect(0, 420, 200, 40), "7b_dungeon_tile_prefabs"))
        {
            SceneManager.LoadScene("7b_dungeon_tile_prefabs");
        }
    }
}
