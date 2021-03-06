﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using UnityEngine.SocialPlatforms;

public class StartGooglePlayGameService : MonoBehaviour {

	// Use this for initialization
	void Start () {

		PlayGamesClientConfiguration config = new PlayGamesClientConfiguration.Builder()
			// enables saving game progress.
			.EnableSavedGames()
			.Build();
		PlayGamesPlatform.InitializeInstance(config);
		// recommended for debugging:
		PlayGamesPlatform.DebugLogEnabled = true;
		// Activate the Google Play Games platform
		PlayGamesPlatform.Activate();

		//login
		Social.localUser.Authenticate((bool success) => {
			if(success){
				ConsoleLog.SLog("login Success");
			} else {
				ConsoleLog.SLog("login Fail");
			}
		});
	}

	public void initializeGPGS(){
		PlayGamesPlatform.DebugLogEnabled = true;
		PlayGamesPlatform.Activate ();
	}
}
