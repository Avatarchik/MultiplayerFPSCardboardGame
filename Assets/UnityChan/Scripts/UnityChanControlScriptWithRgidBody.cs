﻿//
// Mecanimのアニメーションデータが、原点で移動しない場合の Rigidbody付きコントローラ
// サンプル
// 2014/03/13 N.Kobyasahi
//
using UnityEngine;
using System.Collections;

[RequireComponent(typeof (Animator))]
[RequireComponent(typeof (CapsuleCollider))]
[RequireComponent(typeof (Rigidbody))]

public class UnityChanControlScriptWithRgidBody : MonoBehaviour
{	
	
	public float animSpeed = 1.5f;
	public float lookSmoother = 3.0f;
	public bool useCurves = true;
	public float useCurvesHeight = 0.5f;
	public float jumpPower = 3.0f; 
	public int characterType = 1;

	public float forwardSpeedSet;
	public float backwardSpeedSet;
	public float sideSpeedSet; 

	private float forwardSpeed;
	private float backwardSpeed;
	private float sideSpeed; 

	private CapsuleCollider col;
	private Rigidbody rb;
	private Vector3 velocity;
	private Vector3 sideVelocity;

	private float orgColHight;
	private Vector3 orgVectColCenter;

	private bool firstTimeDeadTrigger = true;
	private float deadAnimationTimer = 0.7f;

	//camera
	private GameObject cardboardMain;
	private GameObject cardboardCamera;
	private GameObject characterHead;
	private CardboardHead cardboardHead;
	private Transform cameraPos;
	private Vector3 cameraOffset, bodyToNeckOffset, neckToCameraPosOffset;
	private GameObject gazePointer;
	private int frameCounter = 0;
	private bool isCalculateCamOffset = false;

	//arm movement
	private GameObject rightArm, leftArm, gunEnd;
	public Vector3 manualIdleRightArmOffset;
	public Vector3 manualIdleLeftArmOffset;
	public Vector3 manualAimRightArmOffset;
	public Vector3 manualAimLeftArmOffset;
	private PlayerGameManager playerGameManager;
	private GunProperties gunProp;

	private Vector3 gunRightArmIdleOffset;

	public bool updateEarly;
	private bool isUpdated = false;

	//animation
	private Animator anim;
	private AnimatorStateInfo currentBaseState;

	static int idleState = Animator.StringToHash("Base Layer.pistol idle normal");
	static int locoState = Animator.StringToHash("Base Layer.Locomotion");
	static int jumpState = Animator.StringToHash("Base Layer.Jump");
	static int restState = Animator.StringToHash("Base Layer.Rest");
	static int idleAimState = Animator.StringToHash("Base Layer.pistol idle aim");

	void Awake(){
		ConsoleLog.SLog ("ControlScript Awake()");
		ConsoleLog.SLog ("CharacterType = " + characterType);

		cardboardMain = GameObject.FindGameObjectWithTag ("CardboardMain");
		cardboardCamera = GameObject.FindGameObjectWithTag("PlayerHead");
		characterHead = GameObject.FindGameObjectWithTag ("CharacterHead");
		cardboardHead = cardboardCamera.GetComponent<CardboardHead> ();
		if (!cardboardHead.isCharacterSync) { cardboardHead.ReSyncCharacter (); }
		cameraPos = GameObject.FindGameObjectWithTag ("CameraPos").transform;
		gazePointer = GameObject.FindGameObjectWithTag ("GazePointer");
	}

	void Start ()
	{	
		ConsoleLog.SLog ("ControlScript Start()");

		cameraOffset = cameraPos.position - transform.position;

		anim = GetComponent<Animator>();
		col = GetComponent<CapsuleCollider>();
		rb = GetComponent<Rigidbody>();
		orgColHight = col.height;
		orgVectColCenter = col.center;

		FindComponents ();
	}

	private void FindComponents(){
		playerGameManager = GetComponent<PlayerGameManager> ();

		GameObject[] players = GameObject.FindGameObjectsWithTag ("Player");
		ConsoleLog.SLog ("Player tag count: " + players.Length);
		foreach (GameObject p in players) {
			if (p.GetComponent<PlayerGameManager> () != null) {
				ConsoleLog.SLog ("found real player");
			}
			if (p.GetComponent<RemoteCharacterController> () != null) {
				ConsoleLog.SLog ("found remote player");
			}
		}

		rightArm = GameObject.FindGameObjectWithTag ("RightArm");
		leftArm = GameObject.FindGameObjectWithTag ("LeftArm");
		gunEnd = GameObject.FindGameObjectWithTag ("GunEnd");
		gunRightArmIdleOffset = - gunEnd.transform.rotation.eulerAngles + rightArm.transform.rotation.eulerAngles;

		gunProp = GameObject.FindGameObjectWithTag ("MyGun").GetComponent<GunProperties> ();
	}

	private void CheckNullComponents () {
		//In case of changing character mid game, I might be null game object.

		if (cardboardCamera == null) {ConsoleLog.SLog ("cardboardCamera null");} 
		if (characterHead == null) {ConsoleLog.SLog ("characterHead null");} 
		if (cardboardHead == null) {ConsoleLog.SLog ("cardboardHead null");} 
		if (cardboardMain == null) {ConsoleLog.SLog ("cardboardMain null");} 
		if (cameraPos == null) {ConsoleLog.SLog ("cameraPos null");} 
		if (gazePointer == null) {ConsoleLog.SLog ("gazePointer null");} 
		if (playerGameManager == null) {ConsoleLog.SLog ("playerGameManager null");} 
		if (rightArm == null) {ConsoleLog.SLog ("rightArm null");} 
		if (leftArm == null) {ConsoleLog.SLog ("leftArm null");} 
		if (gunEnd == null) {ConsoleLog.SLog ("gunEnd null");} 
		if (gunProp == null) {ConsoleLog.SLog ("gunProp null");}

		if (
			cardboardCamera == null ||
			characterHead == null ||
			cardboardHead == null ||
			cardboardMain == null ||
			cameraPos == null ||
			gazePointer == null ||

			playerGameManager == null ||
			rightArm == null ||
			leftArm == null ||
			gunEnd == null ||
			gunProp == null
		) {
			Awake();
			Start();
		}
	}

	void FixedUpdate (){
		
		CheckNullComponents ();

		if (MultiplayerController.instance.isGameStart && !playerGameManager.isDead ) {
			Walk ();
			Jump ();
		} else {
			anim.SetFloat("Speed", 0f);
			anim.SetFloat("Direction", 0f);
			if (playerGameManager.isDead){
				if (firstTimeDeadTrigger) {
					rb.freezeRotation = true;
					anim.SetTrigger ("Dead");
					firstTimeDeadTrigger = false;
				}
				if (deadAnimationTimer > 0) {
					deadAnimationTimer -= Time.deltaTime;
				} else if (deadAnimationTimer < 0) {
					deadAnimationTimer = 0;
					col.center = new Vector3 (0f,1.4f,0f);
				} 
			}
		}

		MoveCamera ();
		Locomotion ();
	}

	void Update (){
		isUpdated = false;
		if (updateEarly) UpdateArm ();
	}

	void LateUpdate(){
		CalculateCamOffsetAtFrame (5);
		if (!playerGameManager.isDead) {
			UpdateArm ();
		}
	}

	private void Walk(){
		float h = Input.GetAxis("Horizontal");
		float v = Input.GetAxis("Vertical");
		anim.SetFloat("Speed", v);
		anim.SetFloat("Direction", h);
		anim.speed = animSpeed;
		currentBaseState = anim.GetCurrentAnimatorStateInfo(0);

		//for gravity problem
		RaycastHit hit = new RaycastHit();
		if (Physics.Raycast (transform.position, -Vector3.up, out hit)) {
			if (hit.distance >= 0.1) {
				rb.useGravity = false;
				rb.AddForce(Physics.gravity * rb.mass * 20);
			}
		}

		if (anim.GetBool("Aim")) {
			forwardSpeed = forwardSpeedSet * 0.6f;
			backwardSpeed = backwardSpeedSet * 0.6f;
			sideSpeed = sideSpeedSet * 0.6f;
		} else {
			forwardSpeed = forwardSpeedSet;
			backwardSpeed = backwardSpeedSet;
			sideSpeed = sideSpeedSet;
		}

		velocity = new Vector3(0, 0, v);
		velocity = transform.TransformDirection(velocity);
		if (v > 0.1) {
			velocity *= forwardSpeed;
		} else if (v < -0.1) {
			velocity *= backwardSpeed;
		}

		sideVelocity = new Vector3 (h, 0, 0);
		sideVelocity = transform.TransformDirection (sideVelocity);
		sideVelocity *= sideSpeed;

		transform.localPosition += velocity * Time.fixedDeltaTime;
		transform.localPosition += sideVelocity * Time.fixedDeltaTime;
	}

	private void Jump(){
		if (Input.GetButtonDown("Jump")) {
			if(!anim.IsInTransition(0)) {
				rb.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
				anim.SetTrigger("JumpTrigger");
			}
		}
	}

	private void MoveCamera (){
		neckToCameraPosOffset = cameraPos.transform.transform.position - characterHead.transform.position;
		cameraOffset = bodyToNeckOffset + neckToCameraPosOffset;

		if (playerGameManager.isInAimMode) {
			RaycastHit hitedObject;
			Ray newRay = new Ray (transform.position + cameraOffset,cardboardHead.transform.forward);
			if (Physics.Raycast (newRay, out hitedObject, gunProp.zoomRange)) {
				Debug.DrawLine (transform.position + cameraOffset,hitedObject.point,Color.yellow,0.5f);
				cardboardMain.transform.position = hitedObject.point - cardboardHead.transform.forward;
			} else {
				cardboardMain.transform.position = transform.position + cameraOffset + cardboardHead.transform.forward * gunProp.zoomRange;
			}
		} else {
			cardboardMain.transform.position = transform.position + cameraOffset;
		}
	}

	private void Locomotion (){
		if (currentBaseState.fullPathHash == locoState)
		{
			if(useCurves){
				resetCollider();
			}
		}
		else if(currentBaseState.fullPathHash == jumpState)
		{
			if(!anim.IsInTransition(0))
			{
				if(useCurves){
					float jumpHeight = anim.GetFloat("JumpHeight");
					float gravityControl = anim.GetFloat("GravityControl"); 
					if(gravityControl > 0)
						rb.useGravity = false;

					Ray ray = new Ray(transform.position + Vector3.up, -Vector3.up);
					RaycastHit hitInfo = new RaycastHit();
					if (Physics.Raycast(ray, out hitInfo))
					{
						if (hitInfo.distance > useCurvesHeight)
						{
							col.height = orgColHight - jumpHeight;
							float adjCenterY = orgVectColCenter.y + jumpHeight;
							col.center = new Vector3(0, adjCenterY, 0);
						}
						else
						{
							resetCollider();
						}
					}
				}
			}
		}
		else if (currentBaseState.fullPathHash == idleState)
		{
			if(useCurves){
				resetCollider();
			}
		}
	}

	private void UpdateArm (){
		if (isUpdated) return;

		isUpdated = true;

		//recalculate offset
		gunRightArmIdleOffset = - gunEnd.transform.rotation.eulerAngles + rightArm.transform.rotation.eulerAngles;
		Vector3 gunToGazePosDiff = gazePointer.transform.position - gunEnd.transform.position;

		//apply offset to arm
		Quaternion rightArmRotation = Quaternion.LookRotation (gunToGazePosDiff) * Quaternion.Euler (gunRightArmIdleOffset);
		rightArm.transform.rotation = rightArmRotation;

		//deal with left arm
		if (playerGameManager.isInAimMode) { //aim
			leftArm.transform.rotation = 
				Quaternion.LookRotation (gunToGazePosDiff) *
				Quaternion.Euler (gunRightArmIdleOffset) *
				Quaternion.Euler (manualAimLeftArmOffset);
		} else {
			if (gunProp.isTwoHanded) { //idle, two handed
				leftArm.transform.rotation = 
					Quaternion.LookRotation (gunToGazePosDiff) *
					Quaternion.Euler (gunRightArmIdleOffset) *
					Quaternion.Euler (manualIdleLeftArmOffset);
			} else { //idle, one handed
				
			}
		}

		if (playerGameManager.isInAimMode) {
			
		} else {
			
		}
	}

	private void CalculateCamOffsetAtFrame(int FrameNum){
		if (frameCounter < FrameNum) {
			frameCounter++;
		} else if(!isCalculateCamOffset) {
			isCalculateCamOffset = true;
			bodyToNeckOffset = characterHead.transform.position - transform.position;
			neckToCameraPosOffset = cameraPos.transform.transform.position - characterHead.transform.position;
			cameraOffset = bodyToNeckOffset + neckToCameraPosOffset;
		}
	}

	void OnGUI()
	{
//		GUI.Box(new Rect(Screen.width -260, 10 ,250 ,150), "Interaction");
//		GUI.Label(new Rect(Screen.width -245,30,250,30),"Up/Down Arrow : Go Forwald/Go Back");
//		GUI.Label(new Rect(Screen.width -245,50,250,30),"Left/Right Arrow : Turn Left/Turn Right");
//		GUI.Label(new Rect(Screen.width -245,70,250,30),"Hit Space key while Running : Jump");
//		GUI.Label(new Rect(Screen.width -245,90,250,30),"Hit Spase key while Stopping : Rest");
//		GUI.Label(new Rect(Screen.width -245,110,250,30),"Left Control : Front Camera");
//		GUI.Label(new Rect(Screen.width -245,130,250,30),"Alt : LookAt Camera");
	}

	void resetCollider()
	{
		col.height = orgColHight;
		col.center = orgVectColCenter;
	}
}
