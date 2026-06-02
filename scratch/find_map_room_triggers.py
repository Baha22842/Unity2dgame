import re

scene_path = r"C:\Users\Baha\Platformer for college\Assets\Scenes\SampleScene.unity"

with open(scene_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Находим все блоки MonoBehaviour с MapRoomTrigger
# В Unity YAML MonoBehaviour выглядит так:
# --- !u!114 &ID
# MonoBehaviour:
#   ...
#   m_Script: {fileID: ..., guid: ...} (для MapRoomTrigger)

# Сначала найдем guid для MapRoomTrigger.cs.meta
meta_path = r"C:\Users\Baha\Platformer for college\Assets\Scripts\MapRoomTrigger.cs.meta"
guid = ""
with open(meta_path, 'r', encoding='utf-8') as f:
    for line in f:
        if "guid:" in line:
            guid = line.split("guid:")[1].strip()
            break

print(f"MapRoomTrigger guid: {guid}")

# Разделяем YAML на документы
docs = content.split("--- !u!")
print(f"Total YAML documents: {len(docs)}")

# Карта ID объектов к их именам и компонентам
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
    
    # Имя GameObject
    if class_id == 1: # GameObject
        name_match = re.search(r"m_Name:\s*(.*)", doc)
        if name_match:
            name = name_match.group(1).strip()
            gameobjects[object_id] = {"name": name, "components": []}
            
    # Компонент Transform
    elif class_id == 4: # Transform
        go_match = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", doc)
        if go_match:
            go_id = int(go_match.group(1))
            components[object_id] = {"type": "Transform", "go": go_id}
            
    # Компонент MonoBehaviour
    elif class_id == 114: # MonoBehaviour
        go_match = re.search(r"m_GameObject:\s*\{fileID:\s*(\d+)\}", doc)
        script_match = re.search(r"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([a-fA-F0-9]+)", doc)
        
        go_id = int(go_match.group(1)) if go_match else None
        script_guid = script_match.group(1) if script_match else ""
        
        if script_guid == guid:
            components[object_id] = {"type": "MapRoomTrigger", "go": go_id, "doc": doc}
        else:
            components[object_id] = {"type": "MonoBehaviour", "go": go_id}

# Связываем компоненты с GameObject
for comp_id, comp_info in components.items():
    go_id = comp_info["go"]
    if go_id in gameobjects:
        gameobjects[go_id]["components"].append(comp_info)

# Печатаем все GameObjects с MapRoomTrigger
for go_id, go_info in gameobjects.items():
    has_trigger = False
    for comp in go_info["components"]:
        if comp["type"] == "MapRoomTrigger":
            has_trigger = True
            break
            
    if has_trigger:
        print(f"\nGameObject: {go_info['name']} (ID: {go_id})")
        for comp in go_info["components"]:
            if comp["type"] == "MapRoomTrigger":
                print("Found MapRoomTrigger component:")
                doc_lines = comp["doc"].split('\n')
                for line in doc_lines[:15]:
                    if "roomCameraBounds" in line or "m_Script" in line:
                        print(f"  {line.strip()}")
