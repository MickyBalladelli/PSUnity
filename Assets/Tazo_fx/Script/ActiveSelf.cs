using UnityEngine;
using System.Collections;

public class ActiveSelf : MonoBehaviour {

	Transform[] childs;

	void Awake(){
		childs = GetComponentsInChildren<Transform>();
	}


	void OnEnable(){


		for(int i=0;i<childs.Length;i++){
			childs[i].gameObject.SetActive(true);
		}
		print(childs.Length+"@@@@@@@");
	}
}
