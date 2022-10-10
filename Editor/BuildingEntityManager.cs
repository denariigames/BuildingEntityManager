/**
 * BuildingEntityManager
 * Author: Denarii Games
 * Version: 1.0
 */

#if UNITY_EDITOR
using MySqlConnector;
using System.Collections.Generic;
using System;
using Cysharp.Threading.Tasks;
using LiteNetLibManager;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using MultiplayerARPG;

namespace DenariiGames.Building
{
	public class BuildingEntityManager : EditorWindow
	{
		struct BuildingData
		{
			public string id;
			public string parentId;
			public int entityId;
			public int currentHp;
			public string mapName;
			public Vector3 position;
			public Quaternion rotation;
			public string lockPassword;
			public string extraData;
			
			public BuildingData(bool init = true) {
				id = "";
				parentId = "";
				entityId = 0;
				currentHp = 0;
				mapName = "";
				position = Vector3.zero;
				rotation = Quaternion.identity;
				lockPassword = "";
				extraData = "";
			}
		}

		// PROPERTIES: ----------------------------------------------------------------------------

		bool isValidSettings = false;
		bool isValidSelection = false;
		bool isSaving = false;
		bool showSettings = false;
		string statusMessage = "";
		MessageType statusMessageType = MessageType.None;
		string setting_address = "127.0.0.1";
		string setting_port = "3306";
		string setting_username = "root";
		string setting_password = "";
		string setting_dbName = "mmorpg_kit";
		bool setting_deleteOnSave = false;

		Dictionary<BuildingEntity, int> entityIdCache = new Dictionary<BuildingEntity, int>();

		// INITIALIZERS: --------------------------------------------------------------------------

		[MenuItem("MMORPG KIT/Denarii Games/BuildingEntity Manager")]
		static void Init()
		{
			BuildingEntityManager window = (BuildingEntityManager)EditorWindow.GetWindow(typeof(BuildingEntityManager));
			window.Show();

			//set title and icon
			Texture icon = AssetDatabase.LoadAssetAtPath<Texture>(RootPath + "/Resources/BuildingEntityManager Icon.png");
			GUIContent titleContent = new GUIContent ("BuildingEntity Manager", icon);
			window.titleContent = titleContent;
		}

		public static string RootPath
		{
			get
			{
				string[] path = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets($"t:Script {nameof(BuildingEntityManager)}")[0]).Split("/");
				Array.Resize(ref path, path.Length - 1);
				return string.Join("/", path);
			}
		}

		void OnGUI()
		{
			GUIStyle labelWrapStyle = new GUIStyle(GUI.skin.GetStyle("label")) { wordWrap = true };
			GUIStyle boxStyle = new GUIStyle(GUI.skin.GetStyle("box")) { padding = new RectOffset(10,10,10,10) };

			//settings button
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			var popupStyle = GUI.skin.FindStyle("IconButton");
			var popupIcon = EditorGUIUtility.IconContent("_Popup");
			var buttonRect = EditorGUILayout.GetControlRect(false, 20f, GUILayout.MaxWidth(20f));
			if (GUI.Button(buttonRect, popupIcon, popupStyle))
			{
				statusMessage = "";
				showSettings = !showSettings;
			}
			EditorGUILayout.EndHorizontal();

			//status message
			if (!string.IsNullOrEmpty(statusMessage))
				EditorGUILayout.HelpBox(statusMessage, statusMessageType);

			EditorGUILayout.Space(10);

			if (showSettings || !isValidSettings)
			{
				//mySql settings
				GUILayout.BeginVertical("", boxStyle);
				GUILayout.Label("MySql Settings", EditorStyles.boldLabel);
				setting_address = EditorGUILayout.TextField("Address", setting_address);
				setting_port = EditorGUILayout.TextField("Port", setting_port);
				setting_username = EditorGUILayout.TextField("Username", setting_username);
				setting_password = EditorGUILayout.TextField("Password", setting_password);
				setting_dbName = EditorGUILayout.TextField("Database", setting_dbName);

				if (!string.IsNullOrEmpty(setting_address) && !string.IsNullOrEmpty(setting_port) && !string.IsNullOrEmpty(setting_username) && !string.IsNullOrEmpty(setting_password) && !string.IsNullOrEmpty(setting_dbName))
				{
					EditorGUILayout.BeginHorizontal();
					GUILayout.Label("", GUILayout.Width(150f));
					if (GUILayout.Button("Test Connection"))
						TestConnection();
					EditorGUILayout.EndHorizontal();
				}

				GUILayout.EndVertical();

				//building manager settings
				GUILayout.BeginVertical("", boxStyle);
				GUILayout.Label("Manager Settings", EditorStyles.boldLabel);
				setting_deleteOnSave = GUILayout.Toggle(setting_deleteOnSave, new GUIContent("Delete from Hierarchy after Save", "After saving BuildingEntity to database, delete object from Hierarchy"));
				GUILayout.EndVertical();
			}

			if (!showSettings && isValidSettings)
			{
				isValidSelection = false;

				if (Selection.activeTransform != null)
				{
					GameObject selectedObject = Selection.activeTransform.gameObject;

					//get BuildingEntity
					if (selectedObject.TryGetComponent<BuildingEntity>(out var buildingEntity))
					{
						isValidSelection = true;

						EditorGUILayout.BeginHorizontal();
						GUILayout.Label("BuildingEntity", GUILayout.Width(150f));
						GUILayout.Label(selectedObject.name);
						EditorGUILayout.EndHorizontal();

						//get entityType
						MonoBehaviour component = buildingEntity as MonoBehaviour;
						string[] componentType = component.GetType().ToString().Split('.');
						EditorGUILayout.BeginHorizontal();
						GUILayout.Label("Type", GUILayout.Width(150f));
						GUILayout.Label(componentType[1]);
						EditorGUILayout.EndHorizontal();

						//save button
						EditorGUILayout.Space(10);
						EditorGUILayout.BeginHorizontal();
						GUILayout.Label("", GUILayout.Width(150f));
						if (isSaving)
							GUILayout.Button("Saving Building...");
						else
						{
							if (GUILayout.Button(setting_deleteOnSave ? "Save Building and Delete" : "Save Building"))
								Save(selectedObject);
						}
						EditorGUILayout.EndHorizontal();
					}
				}

				if (!isValidSelection)
					EditorGUILayout.HelpBox("Select BuildingEntity object in Hierarchy to save to database", MessageType.Warning);
			}
		}

		// PUBLIC METHODS: ------------------------------------------------------------------------

		public static BuildingEntityManager Instance
		{
			get { return GetWindow<BuildingEntityManager>(); }
		}

		//callback from editor button
		public async UniTask TestConnection()
		{
			statusMessage = "";
			isValidSettings = false;

			try
			{
				MySqlConnection connection = NewConnection();
				await connection.OpenAsync();
				await connection.CloseAsync();

				//successful connection
				statusMessage = "Connection success";
				statusMessageType = MessageType.None;
				isValidSettings = true;
				showSettings = false;
			}
			catch (MySqlException ex)
			{
				Debug.LogError(ex);
				statusMessage = $"Connection failed";
				statusMessageType = MessageType.Error;
			}
		}

		//callback from editor button
		public async void Save(GameObject selectedObject)
		{
			statusMessage = "";
			isSaving = true;

			if (selectedObject.TryGetComponent<BuildingEntity>(out var buildingEntity))
			{
				BuildingData buildingData = new BuildingData();
				buildingData.id = GetUniqueId();
				buildingData.entityId = GetEntityId(buildingEntity);
				buildingData.currentHp = buildingEntity.MaxHp;
				buildingData.mapName = SceneManager.GetActiveScene().name;
				buildingData.position = selectedObject.transform.position;
				buildingData.rotation = selectedObject.transform.rotation;

				MySqlConnection connection = NewConnection();
				await OpenConnection(connection);

				if (await SaveBuildingData(connection, buildingData) == 1)
				{
					statusMessage = setting_deleteOnSave ? $"Added BuildingEntity {buildingEntity.name} to database." : $"Added BuildingEntity {buildingEntity.name} to database. You can now delete the object from Hierarchy.";
					statusMessageType = MessageType.None;

					if (setting_deleteOnSave)
						DestroyImmediate(selectedObject);
				}
			}

			isSaving = false;
		}

		// PRIVATE METHODS: -----------------------------------------------------------------------

		private int GetEntityId(BuildingEntity obj)
		{
			//return from cache
			if (entityIdCache.ContainsKey(obj)) return entityIdCache[obj];

			int entityId = 0;
			LiteNetLibIdentity identity = obj.transform.GetComponent<LiteNetLibIdentity>();
			if (identity != null) entityId = identity.HashAssetId;

			//add to cache
			entityIdCache.Add(obj, entityId);

			return entityId;
		}

		private async UniTask<int> SaveBuildingData(MySqlConnection connection, BuildingData buildingData)
		{
			string parentId = buildingData.parentId != null ? buildingData.parentId : "";
			int currentHp = buildingData.currentHp > 0 ? buildingData.currentHp : 100;
			string lockPassword = buildingData.lockPassword != null ? buildingData.lockPassword : "";
			string extraData = buildingData.extraData != null ? buildingData.extraData : "";

			return await ExecuteNonQuery(connection, null, "INSERT INTO buildings (id, parentId, entityId, currentHp, mapName, positionX, positionY, positionZ, rotationX, rotationY, rotationZ, lockPassword, extraData) VALUES (@id, @parentId, @entityId, @currentHp, @mapName, @positionX, @positionY, @positionZ, @rotationX, @rotationY, @rotationZ, @lockPassword, @extraData)",
				new MySqlParameter("@id", buildingData.id),
				new MySqlParameter("@parentId", parentId),
				new MySqlParameter("@entityId", buildingData.entityId),
				new MySqlParameter("@currentHp", currentHp),
				new MySqlParameter("@mapName", buildingData.mapName),
				new MySqlParameter("@positionX", buildingData.position.x),
				new MySqlParameter("@positionY", buildingData.position.y),
				new MySqlParameter("@positionZ", buildingData.position.z),
				new MySqlParameter("@rotationX", buildingData.rotation.eulerAngles.x),
				new MySqlParameter("@rotationY", buildingData.rotation.eulerAngles.y),
				new MySqlParameter("@rotationZ", buildingData.rotation.eulerAngles.z),
				new MySqlParameter("@lockPassword", lockPassword),
				new MySqlParameter("@extraData", extraData)
			);
		}

		//==================================================
		//duplicated from Kit:GenericUtils

		private static string GetUniqueId(int length = 12, string mask = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890_-")
		{
			return Nanoid.Nanoid.Generate(mask, length);
		}

		//==================================================
		//duplicated from Kit:MySQLDatabase

		private string GetConnectionString()
		{
			string connectionString = "Server=" + setting_address + ";" +
				"Port=" + setting_port + ";" +
				"Uid=" + setting_username + ";" +
				(string.IsNullOrEmpty(setting_password) ? "" : "Pwd=\"" + setting_password + "\";") +
				"Database=" + setting_dbName + ";" +
				"SSL Mode=None;";
			return connectionString;
		}

		private MySqlConnection NewConnection()
		{
			return new MySqlConnection(GetConnectionString());
		}

		private async UniTask OpenConnection(MySqlConnection connection)
		{
			try
			{
				await connection.OpenAsync();
			}
			catch (MySqlException ex)
			{
				Debug.LogError(ex);
			}
		}

		private async UniTask ExecuteReader(Action<MySqlDataReader> onRead, string sql, params MySqlParameter[] args)
		{
			MySqlConnection connection = NewConnection();
			await OpenConnection(connection);
			await ExecuteReader(connection, null, onRead, sql, args);
			await connection.CloseAsync();
		}

		private async UniTask ExecuteReader(MySqlConnection connection, MySqlTransaction transaction, Action<MySqlDataReader> onRead, string sql, params MySqlParameter[] args)
		{
			bool createLocalConnection = false;
			if (connection == null)
			{
				connection = NewConnection();
				transaction = null;
				await OpenConnection(connection);
				createLocalConnection = true;
			}
			using (MySqlCommand cmd = new MySqlCommand(sql, connection))
			{
				if (transaction != null)
					cmd.Transaction = transaction;
				foreach (MySqlParameter arg in args)
				{
					cmd.Parameters.Add(arg);
				}
				try
				{
					MySqlDataReader dataReader = await cmd.ExecuteReaderAsync();
					if (onRead != null) onRead.Invoke(dataReader);
					dataReader.Close();
				}
				catch (MySqlException ex)
				{
					Debug.LogError(ex);
				}
			}
			if (createLocalConnection)
				await connection.CloseAsync();
		}

		private async UniTask<int> ExecuteNonQuery(string sql, params MySqlParameter[] args)
		{
			MySqlConnection connection = NewConnection();
			await OpenConnection(connection);
			int result = await ExecuteNonQuery(connection, null, sql, args);
			await connection.CloseAsync();
			return result;
		}

		private async UniTask<int> ExecuteNonQuery(MySqlConnection connection, MySqlTransaction transaction, string sql, params MySqlParameter[] args)
		{
			bool createLocalConnection = false;
			if (connection == null)
			{
				connection = NewConnection();
				transaction = null;
				await OpenConnection(connection);
				createLocalConnection = true;
			}
			int numRows = 0;
			using (MySqlCommand cmd = new MySqlCommand(sql, connection))
			{
				if (transaction != null)
					cmd.Transaction = transaction;
				foreach (MySqlParameter arg in args)
				{
					cmd.Parameters.Add(arg);
				}
				try
				{
					numRows = await cmd.ExecuteNonQueryAsync();
				}
				catch (MySqlException ex)
				{
					Debug.LogError(ex);
				}
			}
			if (createLocalConnection)
				await connection.CloseAsync();
			return numRows;
		}
	}
}
#endif