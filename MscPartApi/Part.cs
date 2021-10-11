﻿using MscPartApi.Tools;
using MscPartApi.Trigger;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MscPartApi
{
	public class Part
	{
		private int clampsAdded;
		private bool partFixed;

		internal List<Part> childParts = new List<Part>();
		public string id;
		public PartBaseInfo partBaseInfo;
		public GameObject gameObject;
		internal PartSave partSave;
		internal Vector3 installPosition;
		internal bool uninstallWhenParentUninstalls;
		internal Vector3 installRotation;
		private GameObject parentGameObject;
		private Part parentPart;
		private List<Screw> savedScrews;
		internal Collider collider;
		public TriggerWrapper trigger;

		public Transform transform => gameObject.transform;
		
		private bool usingGameObjectInstantiation;
		private GameObject gameObjectUsedForInstantiation;
		private bool usingPartParent;

		internal List<Action> preInstallActions = new List<Action>();
		internal List<Action> postInstallActions = new List<Action>();

		internal List<Action> preUninstallActions = new List<Action>();
		internal List<Action> postUninstallActions = new List<Action>();
		internal bool screwPlacementMode;
		private Vector3 defaultRotation = Vector3.zero;
		private Vector3 defaultPosition = Vector3.zero;

		private void Setup(string id, string name, GameObject parentGameObject, Vector3 installPosition,
			Vector3 installRotation, PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls,
			bool disableCollisionWhenInstalled, string prefabName)
		{
			this.id = id;
			this.partBaseInfo = partBaseInfo;
			this.installPosition = installPosition;
			this.uninstallWhenParentUninstalls = uninstallWhenParentUninstalls;
			this.installRotation = installRotation;
			this.parentGameObject = parentGameObject;

			if (usingGameObjectInstantiation)
			{
				gameObject = gameObjectUsedForInstantiation;
			}
			else
			{
				gameObject = Helper.LoadPartAndSetName(partBaseInfo.assetBundle, prefabName ?? id, name);
			}

			if (!partBaseInfo.partsSave.TryGetValue(id, out partSave)) {
				partSave = new PartSave();
			}

			savedScrews = new List<Screw>(partSave.screws);
			partSave.screws.Clear();

			collider = gameObject.GetComponent<Collider>();

			trigger = new TriggerWrapper(this, parentGameObject, disableCollisionWhenInstalled);

			if (partSave.installed) {
				Install();
			}

			LoadPartPositionAndRotation(gameObject, partSave);

			if (!MscPartApi.modSaveFileMapping.ContainsKey(partBaseInfo.mod.ID)) {
				MscPartApi.modSaveFileMapping.Add(partBaseInfo.mod.ID, partBaseInfo.saveFilePath);
			}

			if (MscPartApi.modsParts.ContainsKey(partBaseInfo.mod.ID)) {
				MscPartApi.modsParts[partBaseInfo.mod.ID].Add(id, this);
			} else {
				MscPartApi.modsParts.Add(partBaseInfo.mod.ID, new Dictionary<string, Part>
				{
					{id, this}
				});
			}
		}

		public Part(string id, string name, GameObject part, Part parentPart, Vector3 installPosition, Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true, string prefabName = null)
		{
			usingGameObjectInstantiation = true;
			gameObjectUsedForInstantiation = part;

			usingPartParent = true;
			this.parentPart = parentPart;

			Setup(id, name, parentPart.gameObject, installPosition, installRotation, partBaseInfo,
				uninstallWhenParentUninstalls, disableCollisionWhenInstalled, prefabName);
		}

		public Part(string id, string name, GameObject parent, Vector3 installPosition, Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true, string prefabName = null)
		{
			Setup(id, name, parent, installPosition, installRotation, partBaseInfo,
				uninstallWhenParentUninstalls, disableCollisionWhenInstalled, prefabName);
		}

		public Part(string id, string name, Part parentPart, Vector3 installPosition, Vector3 installRotation,
			PartBaseInfo partBaseInfo, bool uninstallWhenParentUninstalls = true,
			bool disableCollisionWhenInstalled = true, string prefabName = null)
		{
			usingPartParent = true;
			this.parentPart = parentPart;
			Setup(id, name, parentPart.gameObject, installPosition, installRotation, partBaseInfo,
				uninstallWhenParentUninstalls, disableCollisionWhenInstalled, prefabName);
			parentPart.childParts.Add(this);
		}

		public void EnableScrewPlacementMode() => screwPlacementMode = true;

		public void SetPosition(Vector3 position)
		{
			if (!IsInstalled()) {
				gameObject.transform.position = position;
			}
		}

		internal void ResetScrews()
		{
			foreach (var screw in partSave.screws) {
				screw.OutBy(screw.tightness);
			}
		}

		public List<Screw> GetScrews()
		{
			return partSave.screws;
		}

		internal void SetScrewsActive(bool active)
		{
			partSave.screws.ForEach(delegate (Screw screw) { screw.gameObject.SetActive(active); });
		}

		public void SetRotation(Quaternion rotation)
		{
			if (!IsInstalled()) {
				gameObject.transform.rotation = rotation;
			}
		}

		public void Install() => trigger.Install();

		public bool IsInstalled()
		{
			return partSave.installed;
		}

		public bool IsFixed()
		{
			return partFixed;
		}

		public void SetFixed(bool partFixed) => this.partFixed = partFixed;

		public void Uninstall()
		{
			trigger.Uninstall();
		}

		public void AddClampModel(Vector3 position, Vector3 rotation, Vector3 scale)
		{
			var clamp = GameObject.Instantiate(MscPartApi.clampModel);
			clamp.name = $"{gameObject.name}_clamp_{clampsAdded}";
			clampsAdded++;
			clamp.transform.SetParent(gameObject.transform);
			clamp.transform.localPosition = position;
			clamp.transform.localScale = scale;
			clamp.transform.localRotation = new Quaternion { eulerAngles = rotation };
		}

		internal bool ParentInstalled()
		{
			if (usingPartParent) {
				return parentPart.IsInstalled();
			} else {
				//Todo: Implement normal msc parts installed/uninstalled
				return true;
			}
		}

		private void LoadPartPositionAndRotation(GameObject gameObject, PartSave partSave)
		{
			SetPosition(partSave.position);
			SetRotation(partSave.rotation);
		}

		public void AddScrew(Screw screw)
		{
			screw.Verify();
			screw.SetPart(this);
			screw.parentCollider = gameObject.GetComponent<Collider>();
			partSave.screws.Add(screw);

			var index = partSave.screws.IndexOf(screw);

			screw.CreateScrewModel(index);

			if (screwPlacementMode) {
				screw.LoadTightness(savedScrews.ElementAtOrDefault(index));
				screw.InBy(screw.tightness, false, true);
			}

			screw.gameObject.SetActive(IsInstalled());

			MscPartApi.screws.Add(screw.gameObject.name, screw);
		}

		public void AddScrews(Screw[] screws, float overrideScale = 0f, float overrideSize = 0f)
		{
			foreach (var screw in screws) {
				if (overrideScale != 0f) {
					screw.scale = overrideScale;
				}

				if (overrideSize != 0f) {
					screw.size = overrideSize;
				}

				AddScrew(screw);
			}
		}

		public void AddPreInstallAction(Action action)
		{
			preInstallActions.Add(action);
		}

		public void AddPostInstallAction(Action action)
		{
			postInstallActions.Add(action);
		}

		public void AddPreUninstallAction(Action action)
		{
			preUninstallActions.Add(action);
		}

		public void AddPostUninstallAction(Action action)
		{
			postUninstallActions.Add(action);
		}


		public T AddWhenInstalledMono<T>() where T : MonoBehaviour
		{
			var mono = AddComponent<T>();
			mono.enabled = IsInstalled();

			AddPostInstallAction(delegate
			{
				mono.enabled = true;
			});

			AddPostUninstallAction(delegate {
				mono.enabled = false;
			});
			return mono;
		}

		public T AddWhenUninstalledMono<T>() where T : MonoBehaviour
		{
			var mono = AddComponent<T>();
			mono.enabled = !IsInstalled();

			AddPostInstallAction(delegate {
				mono.enabled = false;
			});

			AddPostUninstallAction(delegate {
				mono.enabled = true;
			});
			return mono;
		}
		
		public T AddComponent<T>() where T : Component => gameObject.AddComponent(typeof(T)) as T;

		public T GetComponent<T>() => gameObject.GetComponent<T>();

		public void SetBought(bool bought)
		{
			partSave.bought = bought;
		}

		public bool GetBought()
		{
			return partSave.bought;
		}

		public void SetActive(bool active)
		{
			gameObject.SetActive(active);
		}

		public void SetDefaultPosition(Vector3 defaultPosition)
		{
			this.defaultPosition = defaultPosition;
		}

		public void SetDefaultRotation(Vector3 defaultRotation)
		{
			this.defaultRotation = defaultRotation;
		}

		public void ResetToDefault(bool uninstall = false)
		{
			if (uninstall && IsInstalled())
			{
				Uninstall();
			}
			SetPosition(defaultPosition);
			SetRotation(Quaternion.Euler(defaultRotation));
		}
	}
}