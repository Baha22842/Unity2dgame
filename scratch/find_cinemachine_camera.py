import re

scene_path = r"C:\Users\Baha\Platformer for college\Assets\Scenes\SampleScene.unity"

with open(scene_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Разделяем YAML на документы
docs = content.split("--- !u!")
components = {}
gameobjects = {}

for doc in docs:
    if not doc:
        continue
    header_match = re.match(r"^(\d+)\s+&(\d+)", doc)
    if not header_match:
        continue
    
    class_id = int(header_match.group(1))
    object_id = int(header_match.group(2))
    
    # GameObject
    if class_id == 1:
        name_match = re.search(r"m_Name:\s*(.*)", doc)
        if name_match:
            name = name_match.group(1).strip()
            gameobjects[object_id] = {"name": name, "components": []}
            
    # Transform
    elif class_id == 4:
        go_match = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", doc)
        pos_match = re.search(r"m_LocalPosition:\s*\{x:\s*([^,]+),\s*y:\s*([^,]+),\s*z:\s*([^}]+)\}", doc)
        if go_match:
            go_id = int(go_match.group(1))
            pos = (pos_match.group(1), pos_match.group(2), pos_match.group(3)) if pos_match else (0,0,0)
            components[object_id] = {"type": "Transform", "go": go_id, "pos": pos}

# Связываем
for comp_id, comp_info in components.items():
    go_id = comp_info["go"]
    if go_id in gameobjects:
        gameobjects[go_id]["components"].append(comp_info)

# Печатаем позиции CinemachineCamera и Room_1_CameraBounds
for go_id, go_info in gameobjects.items():
    if "Cinemachine" in go_info["name"] or "Bounds" in go_info["name"] or "Camera" in go_info["name"]:
        print(f"\nGameObject: {go_info['name']} (ID: {go_id})")
        for comp in go_info["components"]:
            if comp["type"] == "Transform":
                print(f"  Transform Position: {comp['pos']}")
