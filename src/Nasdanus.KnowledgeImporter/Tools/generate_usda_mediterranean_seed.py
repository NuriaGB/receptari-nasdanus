import csv
import json
import re
import unicodedata
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path


BASE = Path("src/Nasdanus.KnowledgeImporter/Data/FoodData_Central_sr_legacy_food_csv_2018-04")
OUTPUT = Path("src/Nasdanus.KnowledgeImporter/Data/usda-sr-legacy-mediterranean-starter.json")
HOUSEHOLD_CATALOG = Path("input/XupXup/wwwroot/data/ingredients-db.json")

NUTRIENTS = {
    "1008": "calories",
    "1003": "protein",
    "1005": "carbohydrates",
    "1004": "fat",
    "1079": "fibre",
    "2000": "sugar",
    "1093": "sodium",
}

CATEGORY_MAP = {
    "Dairy and Egg Products": ("dairy-eggs", "dairy-eggs", "dairy and eggs"),
    "Spices and Herbs": ("spices", "spices", "spices, dried herbs and flavourings"),
    "Fats and Oils": ("pantry", "pantry", "oils and fats"),
    "Poultry Products": ("meat", "meat", "poultry"),
    "Soups, Sauces, and Gravies": ("pantry", "pantry", "sauces and stocks"),
    "Sausages and Luncheon Meats": ("meat", "meat", "cured meats and sausages"),
    "Fruits and Fruit Juices": ("fruit", "vegetables", "fruit"),
    "Pork Products": ("meat", "meat", "pork"),
    "Vegetables and Vegetable Products": ("vegetables", "vegetables", "vegetables, mushrooms and fresh herbs"),
    "Nut and Seed Products": ("pantry", "pantry", "nuts and seeds"),
    "Beef Products": ("meat", "meat", "beef"),
    "Finfish and Shellfish Products": ("fish", "fish", "fish and seafood"),
    "Legumes and Legume Products": ("legumes", "pantry", "legumes"),
    "Lamb, Veal, and Game Products": ("meat", "meat", "lamb, veal and game"),
    "Baked Products": ("grains", "pantry", "bread and dough ingredients"),
    "Cereal Grains and Pasta": ("grains", "pantry", "rice, cereals and pasta"),
    "Sweets": ("pantry", "pantry", "baking ingredients"),
    "Beverages": ("pantry", "pantry", "beverages and cooking alcohol"),
}

GROUP_TARGETS = {
    "produce": {
        "categories": {"Vegetables and Vegetable Products", "Fruits and Fruit Juices"},
        "target": 130,
    },
    "meat": {
        "categories": {"Poultry Products", "Pork Products", "Beef Products", "Lamb, Veal, and Game Products"},
        "target": 85,
    },
    "fish": {
        "categories": {"Finfish and Shellfish Products"},
        "target": 50,
    },
    "spices": {
        "categories": {"Spices and Herbs", "Soups, Sauces, and Gravies"},
        "target": 65,
    },
    "other": {
        "categories": {
            "Dairy and Egg Products",
            "Legumes and Legume Products",
            "Cereal Grains and Pasta",
            "Baked Products",
            "Nut and Seed Products",
            "Fats and Oils",
            "Beverages",
            "Sausages and Luncheon Meats",
            "Sweets",
        },
        "target": 155,
    },
}

EXCLUDE_PATTERNS = [
    "babyfood",
    "restaurant",
    "fast foods",
    "school lunch",
    "mcdonald",
    "burger king",
    "wendy",
    "kfc",
    "taco bell",
    "subway",
    "pizza hut",
    "pillsbury",
    "kraft",
    "campbell",
    "nestle",
    "general mills",
    "kellogg",
    "quaker",
    "post,",
    "m&m",
    "snacks,",
    "candies,",
    "beverages, fruit-flavored",
    "infant formula",
]

PREFERRED_KEYWORDS = {
    "produce": [
        "tomato", "onion", "garlic", "potato", "carrot", "pepper", "eggplant", "zucchini",
        "squash", "pumpkin", "spinach", "chard", "lettuce", "arugula", "endive", "escarole",
        "cabbage", "cauliflower", "broccoli", "artichoke", "asparagus", "celery", "leek",
        "cucumber", "mushroom", "peas", "green beans", "fennel", "beet", "turnip", "radish",
        "okra", "sweet potato", "olive", "parsley", "cilantro", "basil", "mint", "apple",
        "pear", "orange", "lemon", "lime", "grapefruit", "tangerine", "grape", "fig", "date",
        "apricot", "peach", "nectarine", "plum", "cherry", "strawberry", "raspberry",
        "blueberry", "melon", "watermelon", "pomegranate", "avocado", "banana", "mango",
    ],
    "meat": [
        "chicken", "turkey", "duck", "rabbit", "beef", "veal", "pork", "lamb", "goat",
        "ground", "loin", "shoulder", "leg", "breast", "thigh", "drumstick", "liver",
        "kidney", "heart", "tongue", "tripe", "oxtail", "rib", "shank", "sirloin",
        "tenderloin", "chuck", "brisket",
    ],
    "fish": [
        "cod", "hake", "salmon", "tuna", "sardine", "anchovy", "mackerel", "trout", "sea bass",
        "bass", "snapper", "monkfish", "halibut", "sole", "flounder", "swordfish", "octopus",
        "squid", "clam", "mussel", "shrimp", "prawn", "crab", "lobster", "scallop", "oyster",
    ],
    "spices": [
        "bay leaf", "cumin", "coriander", "cinnamon", "cloves", "cardamom", "fennel seed",
        "anise", "paprika", "pepper", "saffron", "turmeric", "ginger", "oregano", "thyme",
        "rosemary", "basil", "parsley", "mint", "dill", "marjoram", "nutmeg", "mustard",
        "vinegar", "soy sauce", "tomato sauce", "stock", "broth", "honey", "molasses",
    ],
    "other": [
        "milk", "yogurt", "cheese", "egg", "butter", "cream", "ricotta", "mozzarella",
        "parmesan", "lentils", "chickpeas", "beans", "peas", "fava", "soy", "tofu",
        "rice", "pasta", "wheat", "flour", "bread", "couscous", "bulgur", "barley",
        "oats", "cornmeal", "semolina", "almond", "walnut", "hazelnut", "pistachio",
        "pine nut", "sesame", "sunflower", "olive oil", "canola oil", "lard", "sugar",
        "cocoa", "chocolate", "yeast", "gelatin", "ham", "bacon", "sausage", "chorizo",
    ],
}

FORCED_FDC_IDS = [
    "173468",  # Salt, table
    "171413",  # Oil, olive, salad or cooking
    "170931",  # Spices, pepper, black
    "170917",  # Spices, bay leaf
    "175043",  # Leavening agents, yeast, baker's, active dry
    "170591",  # Nuts, pine nuts, dried
    "169246",  # Leeks, raw
    "169074",  # Tomato sauce, canned, no salt added
    "173190",  # Wine, table, red
    "174837",  # Wine, table, white
    "169070",  # Wine, cooking
    "172884",  # Stock, chicken, home-prepared
    "172885",  # Stock, fish, home-prepared
    "172883",  # Stock, beef, home-prepared
    "171583",  # Vegetable broth, ready to serve
    "170286",  # Buckwheat
    "169231",  # Ginger root, raw
    "173471",  # Vanilla extract
    "169698",  # Cornstarch
    "174924",  # Bread crumbs
    "170170",  # Coconut meat, dried, not sweetened
    "170924",  # Curry powder
    "171009",  # Mayonnaise, regular
    "172804",  # Baking powder
    "175040",  # Baking soda
    "169124",  # Pineapple, raw
    "169910",  # Mangos, raw
    "170581",  # Hazelnuts
    "175139",  # Sardines, canned in oil
    "168928",  # Pasta, cooked
    "169593",  # Cocoa powder, unsweetened
    "167995",  # Marshmallows
    "170272",  # Dark chocolate
    "172791",  # Phyllo dough
    "168576",  # Jalapeno peppers, raw
    "172448",  # Tofu, firm
    "172420",  # Lentils, raw
    "169699",  # Couscous, dry
    "168155",  # Limes, raw
    "167818",  # Pork loin, raw
    "172521",  # Rabbit, raw
    "170859",  # Heavy whipping cream
    "171412",  # Coconut oil
    "169998",  # Corn, sweet, yellow, raw
    "169640",  # Honey
    "174223",  # Squid, raw
    "174216",  # Mussels, raw
    "169599",  # Gelatin, dry powder, unsweetened
    "171316",  # Anise seed
]

LOCALIZED_BY_FDC_ID = {
    "173468": ("Sal", "Sal", "Sal"),
    "171413": ("Oli d'oliva", "Oli d'oliva", "Aceite de oliva"),
    "170931": ("Pebre negre", "Pebre negre", "Pimienta negra"),
    "170917": ("Llorer", "Llorer", "Laurel"),
    "175043": ("Llevat sec", "Llevat sec", "Levadura seca"),
    "170591": ("Pinyons", "Pinyons", "Pinones"),
    "169246": ("Porro", "Porro", "Puerro"),
    "169074": ("Salsa de tomàquet", "Salsa de tomàquet", "Salsa de tomate"),
    "173190": ("Vi negre", "Vi negre", "Vino tinto"),
    "174837": ("Vi blanc", "Vi blanc", "Vino blanco"),
    "169070": ("Vi de cuina", "Vi de cuina", "Vino de cocina"),
    "172884": ("Brou de pollastre", "Brou de pollastre", "Caldo de pollo"),
    "172885": ("Brou de peix", "Brou de peix", "Caldo de pescado"),
    "172883": ("Brou de vedella", "Brou de vedella", "Caldo de ternera"),
    "171583": ("Brou de verdures", "Brou de verdures", "Caldo de verduras"),
    "170286": ("Blat sarraí", "Blat sarraí", "Trigo sarraceno"),
    "169231": ("Gingebre fresc", "Gingebre fresc", "Jengibre fresco"),
    "173471": ("Extracte de vainilla", "Extracte de vainilla", "Extracto de vainilla"),
    "169698": ("Midó de blat de moro", "Midó de blat de moro", "Almidon de maiz"),
    "174924": ("Pa ratllat", "Pa ratllat", "Pan rallado"),
    "170170": ("Coco ratllat", "Coco ratllat", "Coco rallado"),
    "170924": ("Curri", "Curri", "Curry"),
    "171009": ("Maionesa", "Maionesa", "Mayonesa"),
    "172804": ("Llevat químic", "Llevat químic", "Levadura quimica"),
    "175040": ("Bicarbonat", "Bicarbonat", "Bicarbonato"),
    "169124": ("Pinya", "Pinya", "Pina"),
    "169910": ("Mango", "Mango", "Mango"),
    "170581": ("Avellana", "Avellana", "Avellana"),
    "175139": ("Sardina en conserva", "Sardina en conserva", "Sardina en conserva"),
    "168928": ("Pasta cuita", "Pasta cuita", "Pasta cocida"),
    "169593": ("Cacau pur", "Cacau pur", "Cacao puro"),
    "167995": ("Marshmallows", "Marshmallows", "Nubes"),
    "170272": ("Xocolata negra", "Xocolata negra", "Chocolate negro"),
    "172791": ("Pasta filo", "Pasta filo", "Pasta filo"),
    "168576": ("Jalapeno", "Jalapeno", "Jalapeno"),
    "172448": ("Tofu", "Tofu", "Tofu"),
    "172420": ("Llenties", "Llenties", "Lentejas"),
    "169699": ("Cuscús", "Cuscús", "Cuscus"),
    "168155": ("Llima", "Llima", "Lima"),
    "167818": ("Llom de porc", "Llom de porc", "Lomo de cerdo"),
    "172521": ("Conill", "Conill", "Conejo"),
    "170859": ("Nata per muntar", "Nata per muntar", "Nata para montar"),
    "171412": ("Oli de coco", "Oli de coco", "Aceite de coco"),
    "169998": ("Blat de moro", "Blat de moro", "Maiz"),
    "169640": ("Mel", "Mel", "Miel"),
    "174223": ("Calamar", "Calamar", "Calamar"),
    "174216": ("Musclos", "Musclos", "Mejillones"),
    "169599": ("Gelatina", "Gelatina", "Gelatina"),
    "171316": ("Anís verd", "Anís verd", "Anis verde"),
}

EXTRA_ALIASES_BY_FDC_ID = {
    "173468": ["sal fina", "sal de taula"],
    "171413": ["oli d'oliva verge extra", "oli oliva", "aceite oliva", "AOVE"],
    "170931": ["pebre", "pimienta", "pimienta negra"],
    "170917": ["fulla de llorer", "laurel"],
    "175043": ["llevat", "levadura", "llevat de forner"],
    "170591": ["pinyó", "pinon"],
    "169074": ["tomate frito", "salsa tomaquet"],
    "173190": ["vino tinto"],
    "174837": ["vino blanco"],
    "169070": ["garnatxa", "vino de cocina"],
    "172884": ["caldo de pollastre", "caldo de pollo", "stock de pollastre"],
    "172885": ["caldo de peix", "caldo de pescado", "fumet"],
    "172883": ["caldo de vedella", "caldo de carne"],
    "171583": ["caldo de verdures", "caldo de verduras", "brou vegetal"],
    "170286": ["blat sarrai", "trigo sarraceno"],
    "169231": ["gingebre", "jengibre"],
    "173471": ["extracte vainilla", "vainilla"],
    "169698": ["maizena", "almidon de maiz", "midó blat de moro"],
    "174924": ["pa ratllat", "pan rallado"],
    "170170": ["coco", "coco ratllat"],
    "170924": ["curry", "curri en pols"],
    "171009": ["mayonesa"],
    "172804": ["llevadura quimica", "impulsor"],
    "175040": ["bicarbonat sodic", "bicarbonato sodico"],
    "169124": ["pineapple"],
    "175139": ["sardina", "sardines"],
    "168928": ["pasta"],
    "169593": ["cacau", "cacao"],
    "167995": ["mini marshmallows", "nuvols", "nubes"],
    "170272": ["xocolata de cobertura", "gotes de xocolata", "chocolate"],
    "172791": ["pasta fullada grega", "phyllo"],
    "168576": ["jalapenos", "jalapenos"],
    "172448": ["tofu ferm"],
    "172420": ["llentia", "lentejas"],
    "169699": ["cous cous", "couscous", "cuscus"],
    "168155": ["lima"],
    "167818": ["llom porc", "lomo cerdo"],
    "170859": ["nata muntar", "nata liquida", "heavy cream"],
    "171412": ["aceite de coco"],
    "169998": ["maiz", "corn"],
    "169640": ["miel"],
    "174223": ["squid"],
    "174216": ["musclo", "mejillon"],
    "169599": ["gelatina neutra"],
    "171316": ["anis", "anís", "matafaluga"],
}

LOCALIZED = {
    "tomato": ("Tomàquet", "Tomate"),
    "onion": ("Ceba", "Cebolla"),
    "garlic": ("All", "Ajo"),
    "potato": ("Patata", "Patata"),
    "carrot": ("Pastanaga", "Zanahoria"),
    "pepper": ("Pebrot", "Pimiento"),
    "eggplant": ("Albergínia", "Berenjena"),
    "zucchini": ("Carbassó", "Calabacín"),
    "pumpkin": ("Carbassa", "Calabaza"),
    "squash": ("Carbassa", "Calabaza"),
    "spinach": ("Espinacs", "Espinacas"),
    "chard": ("Bleda", "Acelga"),
    "lettuce": ("Enciam", "Lechuga"),
    "arugula": ("Ruca", "Rúcula"),
    "endive": ("Endívia", "Endivia"),
    "escarole": ("Escarola", "Escarola"),
    "cabbage": ("Col", "Repollo"),
    "cauliflower": ("Coliflor", "Coliflor"),
    "broccoli": ("Bròquil", "Brócoli"),
    "artichoke": ("Carxofa", "Alcachofa"),
    "asparagus": ("Espàrrec", "Espárrago"),
    "celery": ("Api", "Apio"),
    "leek": ("Porro", "Puerro"),
    "cucumber": ("Cogombre", "Pepino"),
    "mushroom": ("Bolet", "Seta"),
    "peas": ("Pèsols", "Guisantes"),
    "green beans": ("Mongeta verda", "Judía verde"),
    "fennel": ("Fonoll", "Hinojo"),
    "beet": ("Remolatxa", "Remolacha"),
    "turnip": ("Nap", "Nabo"),
    "radish": ("Rave", "Rábano"),
    "okra": ("Okra", "Okra"),
    "sweet potato": ("Moniato", "Boniato"),
    "olive": ("Oliva", "Aceituna"),
    "parsley": ("Julivert", "Perejil"),
    "cilantro": ("Coriandre", "Cilantro"),
    "basil": ("Alfàbrega", "Albahaca"),
    "mint": ("Menta", "Menta"),
    "apple": ("Poma", "Manzana"),
    "pear": ("Pera", "Pera"),
    "orange": ("Taronja", "Naranja"),
    "lemon": ("Llimona", "Limón"),
    "lime": ("Llima", "Lima"),
    "grape": ("Raïm", "Uva"),
    "fig": ("Figa", "Higo"),
    "date": ("Dàtil", "Dátil"),
    "apricot": ("Albercoc", "Albaricoque"),
    "peach": ("Préssec", "Melocotón"),
    "plum": ("Pruna", "Ciruela"),
    "cherry": ("Cirera", "Cereza"),
    "strawberry": ("Maduixa", "Fresa"),
    "melon": ("Meló", "Melón"),
    "watermelon": ("Síndria", "Sandía"),
    "pomegranate": ("Magrana", "Granada"),
    "avocado": ("Alvocat", "Aguacate"),
    "banana": ("Plàtan", "Plátano"),
    "chicken": ("Pollastre", "Pollo"),
    "turkey": ("Gall dindi", "Pavo"),
    "duck": ("Ànec", "Pato"),
    "rabbit": ("Conill", "Conejo"),
    "beef": ("Vedella", "Ternera"),
    "veal": ("Vedella", "Ternera"),
    "pork": ("Porc", "Cerdo"),
    "lamb": ("Xai", "Cordero"),
    "goat": ("Cabrit", "Cabrito"),
    "liver": ("Fetge", "Hígado"),
    "kidney": ("Ronyó", "Riñón"),
    "heart": ("Cor", "Corazón"),
    "tongue": ("Llengua", "Lengua"),
    "tripe": ("Capipota", "Callos"),
    "cod": ("Bacallà", "Bacalao"),
    "hake": ("Lluç", "Merluza"),
    "salmon": ("Salmó", "Salmón"),
    "tuna": ("Tonyina", "Atún"),
    "sardine": ("Sardina", "Sardina"),
    "anchovy": ("Anxova", "Anchoa"),
    "mackerel": ("Verat", "Caballa"),
    "trout": ("Truita de riu", "Trucha"),
    "sea bass": ("Llobarro", "Lubina"),
    "monkfish": ("Rap", "Rape"),
    "sole": ("Llenguado", "Lenguado"),
    "swordfish": ("Peix espasa", "Pez espada"),
    "octopus": ("Pop", "Pulpo"),
    "squid": ("Calamar", "Calamar"),
    "clam": ("Cloïssa", "Almeja"),
    "mussel": ("Musclo", "Mejillón"),
    "shrimp": ("Gamba", "Gamba"),
    "prawn": ("Llagostí", "Langostino"),
    "crab": ("Cranc", "Cangrejo"),
    "lobster": ("Llamàntol", "Bogavante"),
    "scallop": ("Vieira", "Vieira"),
    "oyster": ("Ostra", "Ostra"),
    "cumin": ("Comí", "Comino"),
    "coriander": ("Coriandre", "Cilantro"),
    "cinnamon": ("Canyella", "Canela"),
    "cloves": ("Clau d'olor", "Clavo"),
    "cardamom": ("Cardamom", "Cardamomo"),
    "anise": ("Anís", "Anís"),
    "paprika": ("Pebre vermell", "Pimentón"),
    "saffron": ("Safrà", "Azafrán"),
    "turmeric": ("Cúrcuma", "Cúrcuma"),
    "ginger": ("Gingebre", "Jengibre"),
    "oregano": ("Orenga", "Orégano"),
    "thyme": ("Farigola", "Tomillo"),
    "rosemary": ("Romaní", "Romero"),
    "dill": ("Anet", "Eneldo"),
    "nutmeg": ("Nou moscada", "Nuez moscada"),
    "mustard": ("Mostassa", "Mostaza"),
    "vinegar": ("Vinagre", "Vinagre"),
    "soy sauce": ("Salsa de soja", "Salsa de soja"),
    "honey": ("Mel", "Miel"),
    "milk": ("Llet", "Leche"),
    "yogurt": ("Iogurt", "Yogur"),
    "cheese": ("Formatge", "Queso"),
    "egg": ("Ou", "Huevo"),
    "butter": ("Mantega", "Mantequilla"),
    "cream": ("Nata", "Nata"),
    "ricotta": ("Ricotta", "Ricotta"),
    "mozzarella": ("Mozzarella", "Mozzarella"),
    "parmesan": ("Parmesà", "Parmesano"),
    "lentils": ("Llenties", "Lentejas"),
    "chickpeas": ("Cigrons", "Garbanzos"),
    "beans": ("Mongetes", "Alubias"),
    "tofu": ("Tofu", "Tofu"),
    "rice": ("Arròs", "Arroz"),
    "pasta": ("Pasta", "Pasta"),
    "flour": ("Farina", "Harina"),
    "bread": ("Pa", "Pan"),
    "couscous": ("Cuscús", "Cuscús"),
    "bulgur": ("Bulgur", "Bulgur"),
    "barley": ("Ordi", "Cebada"),
    "oats": ("Civada", "Avena"),
    "semolina": ("Sèmola", "Sémola"),
    "almond": ("Ametlla", "Almendra"),
    "walnut": ("Nou", "Nuez"),
    "hazelnut": ("Avellana", "Avellana"),
    "pistachio": ("Pistatxo", "Pistacho"),
    "pine nut": ("Pinyó", "Piñón"),
    "sesame": ("Sèsam", "Sésamo"),
    "sunflower": ("Gira-sol", "Girasol"),
    "olive oil": ("Oli d'oliva", "Aceite de oliva"),
    "sugar": ("Sucre", "Azúcar"),
    "cocoa": ("Cacau", "Cacao"),
    "chocolate": ("Xocolata", "Chocolate"),
    "yeast": ("Llevat", "Levadura"),
    "gelatin": ("Gelatina", "Gelatina"),
    "ham": ("Pernil", "Jamón"),
    "bacon": ("Cansalada", "Panceta"),
    "sausage": ("Botifarra", "Salchicha"),
    "chorizo": ("Xoriço", "Chorizo"),
}


def read_csv(name):
    with open(BASE / name, newline="", encoding="utf-8-sig") as handle:
        return list(csv.DictReader(handle))


def norm(value):
    normalized = unicodedata.normalize("NFD", value.lower())
    without_marks = "".join(
        character
        for character in normalized
        if unicodedata.category(character) != "Mn"
    )
    return re.sub(r"[^a-z0-9 ]+", " ", without_marks).strip()


def contains_any(value, patterns):
    n = norm(value)
    return any(pattern in n for pattern in patterns)


def clean_description(description):
    parts = [part.strip() for part in description.split(",")]
    keep = []
    for part in parts:
        lowered = part.lower()
        if lowered in {"raw", "cooked", "boiled", "baked", "roasted", "fried", "broiled", "steamed"}:
            continue
        if any(token in lowered for token in ["with salt", "without salt", "with skin", "without skin"]):
            continue
        keep.append(part)
        if len(keep) >= 3:
            break
    return ", ".join(keep).strip().title()


def best_localized(description, fdc_id):
    if fdc_id in LOCALIZED_BY_FDC_ID:
        return LOCALIZED_BY_FDC_ID[fdc_id]

    n = norm(description)
    matches = sorted(
        ((keyword, names) for keyword, names in LOCALIZED.items() if keyword in n),
        key=lambda item: len(item[0]),
        reverse=True,
    )
    if not matches:
        display = clean_description(description)
        return display, display, display

    keyword, (ca, es) = matches[0]
    suffix = []
    for token, label_ca, label_es in [
        ("raw", "cru", "crudo"),
        ("cooked", "cuit", "cocido"),
        ("boiled", "bullit", "hervido"),
        ("roasted", "rostit", "asado"),
        ("dried", "sec", "seco"),
        ("canned", "en conserva", "en conserva"),
        ("smoked", "fumat", "ahumado"),
        ("ground", "picat", "picado"),
    ]:
        if token in n:
            suffix.append((label_ca, label_es))
            break
    if suffix:
        return f"{ca} {suffix[0][0]}", f"{ca} {suffix[0][0]}", f"{es} {suffix[0][1]}"
    return ca, ca, es


def localized_base_names(description):
    n = norm(description)
    names = set()
    for keyword, (ca, es) in LOCALIZED.items():
        if keyword in n:
            names.add(ca)
            names.add(es)
    return names


def load_household_aliases():
    if not HOUSEHOLD_CATALOG.exists():
        return {}

    payload = json.loads(HOUSEHOLD_CATALOG.read_text(encoding="utf-8"))
    aliases_by_key = defaultdict(set)
    for item in payload.get("ingredients", []):
        names = [item.get("nom", "")] + item.get("sinonims", [])
        normalized_names = {norm(name) for name in names if name}
        for key in normalized_names:
            aliases_by_key[key].update(name for name in names if name)
    return aliases_by_key


def prune_duplicate_aliases(items):
    identity_keys = set()
    for item in items:
        for field in ["name", "catalanName", "spanishName"]:
            key = norm(item[field])
            if key:
                identity_keys.add(key)

    alias_owner = {}
    for item in items:
        own_identity_keys = {
            norm(item["name"]),
            norm(item["catalanName"]),
            norm(item["spanishName"]),
        }
        cleaned = []
        for alias in item["aliases"]:
            key = norm(alias)
            if not key or key in own_identity_keys:
                continue
            if key in identity_keys:
                continue
            if key in alias_owner:
                continue

            alias_owner[key] = item["providerId"]
            cleaned.append(alias)

        item["aliases"] = sorted(cleaned)


def nutrition_state(description):
    n = norm(description)
    if "raw" in n:
        return "raw"
    if "cooked" in n or "boiled" in n or "roasted" in n or "steamed" in n or "fried" in n:
        return "cooked"
    if "dried" in n or "dry" in n:
        return "dry"
    if "canned" in n:
        return "canned"
    if "smoked" in n:
        return "smoked"
    return "unspecified"


def default_unit(category):
    return "ml" if category == "Fats and Oils" else "g"


def group_for_category(category):
    for group, spec in GROUP_TARGETS.items():
        if category in spec["categories"]:
            return group
    return None


def base_description_key(description):
    key = norm(description)
    key = re.sub(r"\b(raw|cooked|boiled|roasted|steamed|fried|with|without|salt|fat|lean)\b", "", key)
    return re.sub(r"\s+", " ", key).strip()


def score_food(row, group):
    description = row["description"]
    n = norm(description)
    score = 0
    for index, keyword in enumerate(PREFERRED_KEYWORDS[group]):
        if keyword in n:
            score += 500 - index
    if "raw" in n:
        score += 150
    if "fresh" in n:
        score += 80
    if "cooked" in n or "boiled" in n or "roasted" in n or "steamed" in n:
        score += 40
    if "canned" in n:
        score -= 25
    if "prepared" in n or "ready-to" in n or "mix" in n:
        score -= 120
    if "with salt" in n or "with sauce" in n or "with cheese" in n:
        score -= 80
    score -= len(description) / 12
    return score


def main():
    categories = {row["id"]: row["description"] for row in read_csv("food_category.csv")}
    ndb = {row["fdc_id"]: row["NDB_number"] for row in read_csv("sr_legacy_food.csv")}
    foods = read_csv("food.csv")
    foods_by_id = {row["fdc_id"]: row for row in foods}
    household_aliases = load_household_aliases()

    nutrient_by_food = defaultdict(dict)
    for row in read_csv("food_nutrient.csv"):
        nutrient_name = NUTRIENTS.get(row["nutrient_id"])
        if not nutrient_name or row["amount"] == "":
            continue
        nutrient_by_food[row["fdc_id"]][nutrient_name] = float(row["amount"])

    candidates_by_group = defaultdict(list)
    for row in foods:
        category = categories.get(row["food_category_id"], "")
        if category not in CATEGORY_MAP:
            continue
        if contains_any(row["description"], EXCLUDE_PATTERNS):
            continue
        nutrition = nutrient_by_food.get(row["fdc_id"], {})
        if not all(key in nutrition for key in ["calories", "protein", "carbohydrates", "fat"]):
            continue
        group = group_for_category(category)
        if group is not None:
            candidates_by_group[group].append((score_food(row, group), row, category))

    selected = []
    selected_ids = set()
    seen_description_keys = set()
    selected_count_by_group = defaultdict(int)

    def add_selected(row, category, group):
        if row["fdc_id"] in selected_ids:
            return False
        base_key = base_description_key(row["description"])
        if base_key in seen_description_keys:
            return False
        selected.append((row, category, group))
        selected_ids.add(row["fdc_id"])
        seen_description_keys.add(base_key)
        selected_count_by_group[group] += 1
        return True

    for fdc_id in FORCED_FDC_IDS:
        row = foods_by_id.get(fdc_id)
        if row is None:
            continue
        category = categories.get(row["food_category_id"], "")
        group = group_for_category(category)
        if category not in CATEGORY_MAP or group is None:
            continue
        add_selected(row, category, group)

    for group, spec in GROUP_TARGETS.items():
        group_candidates = sorted(candidates_by_group[group], key=lambda value: value[0], reverse=True)
        taken = selected_count_by_group[group]
        for _, row, category in group_candidates:
            if not add_selected(row, category, group):
                continue
            taken += 1
            if taken >= spec["target"]:
                break

    items = []
    seen_names = defaultdict(int)
    for row, category, group in selected:
        mapped_category, pantry_category, subcategory = CATEGORY_MAP[category]
        canonical, catalan, spanish = best_localized(row["description"], row["fdc_id"])
        seen_names[canonical] += 1
        if seen_names[canonical] > 1:
            suffix = seen_names[canonical]
            canonical = f"{canonical} {suffix}"
            catalan = f"{catalan} {suffix}"
            spanish = f"{spanish} {suffix}"

        nutrition = nutrient_by_food[row["fdc_id"]]
        sodium = nutrition.get("sodium")
        aliases = {
            row["description"],
            *localized_base_names(row["description"]),
            *EXTRA_ALIASES_BY_FDC_ID.get(row["fdc_id"], []),
        }
        for alias in list(aliases):
            aliases.update(household_aliases.get(norm(alias), set()))
        aliases = sorted(aliases)
        item = {
            "providerId": f"fdc:{row['fdc_id']}",
            "fdcId": row["fdc_id"],
            "ndbNumber": ndb.get(row["fdc_id"], ""),
            "name": canonical,
            "catalanName": catalan,
            "spanishName": spanish,
            "aliases": aliases,
            "category": mapped_category,
            "subcategory": subcategory,
            "defaultUnit": default_unit(category),
            "canFreeze": mapped_category in {"meat", "fish"} or "bread" in norm(row["description"]),
            "pantryCategory": pantry_category,
            "nutritionState": nutrition_state(row["description"]),
            "nutrition": {
                "calories": round(nutrition["calories"], 2),
                "protein": round(nutrition["protein"], 2),
                "carbohydrates": round(nutrition["carbohydrates"], 2),
                "fat": round(nutrition["fat"], 2),
                **({"fibre": round(nutrition["fibre"], 2)} if "fibre" in nutrition else {}),
                **({"sugar": round(nutrition["sugar"], 2)} if "sugar" in nutrition else {}),
                **({"salt": round(sodium * 2.5 / 1000, 3)} if sodium is not None else {}),
            },
        }
        items.append(item)

    items.sort(key=lambda item: (item["category"], item["subcategory"], item["name"]))
    prune_duplicate_aliases(items)
    payload = {
        "schemaVersion": 1,
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "source": "USDA FoodData Central SR Legacy",
        "sourceRelease": "2018-04",
        "sourceUrl": "https://fdc.nal.usda.gov/download-datasets",
        "notes": [
            "Nutrition values are copied from USDA FoodData Central SR Legacy per 100 g.",
            "Salt is derived from USDA sodium using sodium_mg * 2.5 / 1000.",
            "Catalan and Spanish names are local curation metadata; USDA descriptions are preserved as aliases."
        ],
        "items": items,
    }
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    counts = defaultdict(int)
    for item in items:
        counts[item["category"]] += 1
    print(f"wrote {len(items)} items to {OUTPUT}")
    for key in sorted(counts):
        print(key, counts[key])


if __name__ == "__main__":
    main()
