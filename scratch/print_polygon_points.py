import re

scene_path = r"C:\Users\Baha\Platformer for college\Assets\Scenes\SampleScene.unity"

with open(scene_path, 'r', encoding='utf-8', errors='ignore') as f:
    content = f.read()

# Разделяем YAML на документы
docs = content.split("--- !u!")

poly_doc = ""
for doc in docs:
    if "2100233972" in doc and "PolygonCollider2D" in doc:
        poly_doc = doc
        break

if poly_doc:
    print("Found PolygonCollider2D (2100233972):")
    # Извлекаем пути и вершины
    # Вершины в Unity YAML для PolygonCollider2D лежат в секции m_Paths
    # Пример структуры:
    # m_Paths:
    # - - {x: -15, y: -10}
    #   - {x: 15, y: -10}
    #   ...
    
    paths_section = False
    points = []
    
    for line in poly_doc.split('\n'):
        if "m_Paths:" in line:
            paths_section = True
            continue
        if paths_section:
            if "m_UseReceiver" in line or "m_Sensor" in line or "m_Trigger" in line:
                break
            # Ищем координаты {x: ..., y: ...}
            match = re.search(r"x:\s*([^,]+),\s*y:\s*([^}]+)", line)
            if match:
                x = float(match.group(1))
                y = float(match.group(2))
                points.append((x, y))
                
    print(f"Total points: {len(points)}")
    transform_pos = (-23.7, 16.8)
    print(f"Transform Local Position of GameObject: {transform_pos}")
    print("Points in World Coordinates:")
    min_x, max_x = float('inf'), float('-inf')
    min_y, max_y = float('inf'), float('-inf')
    
    for i, pt in enumerate(points):
        world_x = pt[0] + transform_pos[0]
        world_y = pt[1] + transform_pos[1]
        print(f"  Point {i}: Local({pt[0]}, {pt[1]}) -> World({world_x:.2f}, {world_y:.2f})")
        min_x = min(min_x, world_x)
        max_x = max(max_x, world_x)
        min_y = min(min_y, world_y)
        max_y = max(max_y, world_y)
        
    print(f"\nWorld Bounds of the Room Polygon:")
    print(f"  X: [{min_x:.2f}, {max_x:.2f}] (Width: {max_x - min_x:.2f})")
    print(f"  Y: [{min_y:.2f}, {max_y:.2f}] (Height: {max_y - min_y:.2f})")
else:
    print("Could not find PolygonCollider2D 2100233972")
