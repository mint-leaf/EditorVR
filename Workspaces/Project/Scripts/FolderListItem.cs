﻿using System;
using System.Collections;
using System.IO;
using ListView;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.VR.Handles;
using UnityEngine.VR.Utilities;
using Object = UnityEngine.Object;

public class FolderListItem : ListViewItem<FolderData>
{
	private const float kMargin = 0.01f;
	private const float kIndent = 0.02f;

	private const float kMagnetizeDuration = 0.75f;
	private const float kExpandArrowRotateSpeed = 0.4f;
	private readonly Vector3 kGrabOffset = new Vector3(0, 0.02f, 0.03f);

	[SerializeField]
	private Text m_Text;

	[SerializeField]
	private BaseHandle m_Cube;

	[SerializeField]
	private BaseHandle m_ExpandArrow;

	[SerializeField]
	private Material m_NoClipCubeMaterial;

	[SerializeField]
	private Material m_NoClipExpandArrowMaterial;

	[SerializeField]
	private Color m_HoverColor;

	[SerializeField]
	private Color m_SelectedColor;

	private Color m_NormalColor;

	private bool m_Hovering;

	private Renderer m_CubeRenderer;

	private Transform m_GrabbedObject;
	private float m_GrabLerp;

	public Action<FolderData> selectFolder;

	public override void Setup(FolderData listData)
	{
		base.Setup(listData);
		// First time setup
		if (m_CubeRenderer == null)
		{
			// Cube material might change, so we always instance it
			m_CubeRenderer = m_Cube.GetComponent<Renderer>();
			m_NormalColor = m_CubeRenderer.sharedMaterial.color;
			U.Material.GetMaterialClone(m_CubeRenderer);

			m_ExpandArrow.handleDragged += ToggleExpanded;
			m_Cube.handleDragging += GrabBegin;
			m_Cube.handleDrag += GrabDrag;
			m_Cube.handleDragged += GrabEnd;

			m_Cube.hovering += HoverBegin;
			m_Cube.hovered += HoverEnd;
		}

		m_Text.text = Path.GetFileName(listData.path);
		m_ExpandArrow.gameObject.SetActive(listData.children != null);
		m_Hovering = false;
	}

	public void SwapMaterials(Material textMaterial, Material expandArrowMaterial)
	{
		m_Text.material = textMaterial;
		m_ExpandArrow.GetComponent<Renderer>().sharedMaterial = expandArrowMaterial;
	}

	public void UpdateTransforms(float width)
	{
		Vector3 cubeScale = m_Cube.transform.localScale;
		cubeScale.x = width;
		m_Cube.transform.localScale = cubeScale;

		var arrowWidth = m_ExpandArrow.transform.localScale.x * 0.5f;
		var halfWidth = width * 0.5f;
		var indent = kIndent * data.treeDepth;
		var doubleMargin = kMargin * 2;
		m_ExpandArrow.transform.localPosition = new Vector3(kMargin + indent - halfWidth, m_ExpandArrow.transform.localPosition.y, 0);

		m_Text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, (width - doubleMargin - indent) * 1 / m_Text.transform.localScale.x);
		// Text is next to arrow, with a margin and indent
		m_Text.transform.localPosition = new Vector3(doubleMargin + indent + arrowWidth - halfWidth, m_Text.transform.localPosition.y, 0);

		var cameraTransform = U.Camera.GetMainCamera().transform;

		Vector3 eyeVector3 = Quaternion.Inverse(transform.parent.rotation) * cameraTransform.forward;
		eyeVector3.x = 0;
		m_Text.transform.localRotation = Quaternion.LookRotation(eyeVector3, 
										Vector3.Dot(eyeVector3, Vector3.forward) > 0 ? Vector3.up : Vector3.down);

		// Rotate arrow for expand state
		m_ExpandArrow.transform.localRotation = Quaternion.Lerp(m_ExpandArrow.transform.localRotation,
											Quaternion.AngleAxis(90f, Vector3.right) * (data.expanded ? Quaternion.AngleAxis(90f, Vector3.back) : Quaternion.identity),
											kExpandArrowRotateSpeed);

		// Set selected/hover/normal color
		if (data.selected)
			m_CubeRenderer.sharedMaterial.color = m_SelectedColor;
		else if(m_Hovering)
			m_CubeRenderer.sharedMaterial.color = m_HoverColor;
		else
			m_CubeRenderer.sharedMaterial.color = m_NormalColor;
	}

	public void GetMaterials(out Material textMaterial, out Material expandArrowMaterial)
	{
		textMaterial = Object.Instantiate(m_Text.material);
		expandArrowMaterial = Object.Instantiate(m_ExpandArrow.GetComponent<Renderer>().sharedMaterial);
	}

	public void Clip(Bounds bounds, Matrix4x4 parentMatrix)
	{
		m_CubeRenderer.sharedMaterial.SetMatrix("_ParentMatrix", parentMatrix);
		m_CubeRenderer.sharedMaterial.SetVector("_ClipExtents", bounds.extents);
	}

	private void ToggleExpanded(BaseHandle handle, HandleEventData eventData)
	{
		data.expanded = !data.expanded;
	}

	private void GrabBegin(BaseHandle baseHandle, HandleEventData eventData)
	{
		if (eventData.direct)
		{
			var clone = (GameObject) Instantiate(gameObject, transform.position, transform.rotation, transform.parent);
			var cloneItem = clone.GetComponent<FolderListItem>();
			cloneItem.m_Cube.GetComponent<Renderer>().sharedMaterial = m_NoClipCubeMaterial;
			cloneItem.m_ExpandArrow.GetComponent<Renderer>().sharedMaterial = m_NoClipExpandArrowMaterial;
			cloneItem.m_Text.material = null;

			m_GrabbedObject = clone.transform;
			m_GrabLerp = 0;
			StartCoroutine(Magnetize());
		}
		else
		{
			var folderItem = baseHandle.GetComponentInParent<FolderListItem>();
			selectFolder(folderItem.data);
		}
	}

	private IEnumerator Magnetize()
	{
		var startTime = Time.realtimeSinceStartup;
		var currTime = 0f;
		while (currTime < kMagnetizeDuration)
		{
			currTime = Time.realtimeSinceStartup - startTime;
			m_GrabLerp = currTime / kMagnetizeDuration;
			yield return null;
		}
		m_GrabLerp = 1;
	}

	private void GrabDrag(BaseHandle baseHandle, HandleEventData eventData)
	{
		if (m_GrabbedObject)
		{
			var rayTransform = eventData.rayOrigin.transform;
			m_GrabbedObject.transform.position = Vector3.Lerp(m_GrabbedObject.transform.position, rayTransform.position + rayTransform.rotation * kGrabOffset, m_GrabLerp);
			m_GrabbedObject.transform.rotation = Quaternion.Lerp(m_GrabbedObject.transform.rotation, rayTransform.rotation, m_GrabLerp);
		}
	}

	private void GrabEnd(BaseHandle baseHandle, HandleEventData eventData)
	{
		if(m_GrabbedObject)
			U.Object.Destroy(m_GrabbedObject.gameObject);
	}

	private void HoverBegin(BaseHandle baseHandle, HandleEventData eventData)
	{
		m_Hovering = true;
	}

	private void HoverEnd(BaseHandle baseHandle, HandleEventData eventData)
	{
		m_Hovering = false;
	}

	private void OnDestroy()
	{
		if (m_CubeRenderer)
			U.Object.Destroy(m_CubeRenderer.sharedMaterial);
	}
}