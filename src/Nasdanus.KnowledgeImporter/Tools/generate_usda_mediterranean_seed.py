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
    "ice cream",
    "frozen yogurt",
    "frostings,",
    "salad dressing",
    "cheese substitute",
    "imitation",
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
    "167746",  # Lemons, raw, without peel
    "167747",  # Lemon juice, raw
    "168462",  # Spinach, raw
    "169705",  # Oats
    "169094",  # Olives, ripe, canned
    "169096",  # Olives, pickled, canned or bottled, green
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
    "171304",  # Greek yogurt, plain, whole milk
    "171265",  # Whole milk
    "170857",  # Light cream
    "173410",  # Butter, salted
    "173430",  # Butter, without salt
    "171287",  # Egg, whole, raw, fresh
    "170848",  # Parmesan, hard
    "171247",  # Parmesan, grated
    "173431",  # Parmesan, shredded
    "170845",  # Mozzarella, whole milk
    "170900",  # Mozzarella, shredded
    "171251",  # Swiss cheese
    "173414",  # Cheddar
    "170899",  # Cheddar, sliced
    "171241",  # Gouda
    "173420",  # Feta
    "172175",  # Blue cheese
    "173418",  # Cream cheese
    "173435",  # Goat cheese, soft
    "173433",  # Goat cheese, semisoft
    "172197",  # Goat cheese, hard
    "170851",  # Ricotta, whole milk
    "168000",  # Chocolate hazelnut spread
    "170273",  # Dark chocolate, 70-85%
    "174183",  # Anchovy, canned in oil
    "173708",  # Tuna, light, canned in oil
    "174190",  # Cod, dried and salted
    "173687",  # Smoked salmon
    "175119",  # Atlantic mackerel, raw
    "174215",  # Cuttlefish, raw
    "174214",  # Clams, raw
    "171509",  # Chicken breast, raw
    "172378",  # Chicken leg quarter, raw
    "172876",  # Turkey breast, raw
    "172941",  # Turkey breast, sliced, prepackaged
    "172408",  # Duck, meat and skin, raw
    "168312",  # Pork tenderloin, raw
    "168263",  # Pork loin chop, raw
    "168295",  # Cured ham, boneless, unheated
    "173859",  # Chorizo, raw
    "174603",  # Salami, Italian, pork
    "169542",  # Beef tenderloin, raw
    "168693",  # Beef shoulder, raw
    "169928",  # Peach, raw
    "169914",  # Nectarine, raw
    "174683",  # Seedless-type grapes
    "167793",  # Fuji apple
    "169911",  # Honeydew melon
    "167765",  # Watermelon
    "169097",  # Orange
    "169105",  # Tangerine/mandarin
    "168195",  # Clementine
    "171705",  # Avocado
    "169988",  # Celery
    "170000",  # Onion, raw
    "170008",  # Sweet onion
    "170005",  # Spring onion
    "169230",  # Garlic
    "170393",  # Carrots
    "170379",  # Broccoli
    "169291",  # Zucchini
    "169228",  # Eggplant
    "170457",  # Tomato, raw
    "168429",  # Butterhead lettuce
    "169249",  # Green leaf lettuce
    "168431",  # Red leaf lettuce
    "170427",  # Green pepper
    "170108",  # Red pepper
    "169756",  # Long grain white rice, raw
    "168931",  # Short grain white rice, raw
    "168927",  # Dry pasta
    "169731",  # Egg noodles, dry
    "169742",  # Rice noodles, dry
    "169727",  # Fresh refrigerated pasta
    "167535",  # Flour tortillas/wraps
    "168936",  # All-purpose flour
    "168896",  # Bread flour
    "168893",  # Whole-grain wheat flour
    "169745",  # Spelt
    "169695",  # Yellow corn flour
    "169697",  # Yellow cornmeal
    "168929",  # Polenta-style cornmeal
    "170187",  # Walnuts
    "170567",  # Almonds
    "170556",  # Pumpkin seeds
    "170150",  # Sesame seeds
    "168874",  # Quinoa, uncooked
    "170554",  # Chia seeds
    "169414",  # Flaxseed
    "171401",  # Lard
    "171017",  # Sunflower oil
    "172241",  # Balsamic vinegar
    "172240",  # Red wine vinegar
    "173469",  # Cider vinegar
    "171610",  # Worcestershire sauce
    "172234",  # Prepared mustard
    "168556",  # Catsup/ketchup
    "171186",  # Sriracha
    "168746",  # Beer
    "171890",  # Brewed coffee
    "169655",  # Granulated sugar
    "168833",  # Brown sugar
    "170674",  # Turbinado sugar
    "169409",  # Coconut milk
    "171711",  # Blueberries
    "167762",  # Strawberries
    "167755",  # Raspberries
    "173946",  # Blackberries
    "170645",  # Apricot jam
    "169641",  # Jams and preserves
    "172688",  # Whole-wheat sandwich bread
    "174924",  # White bread / bread crumbs
    "172675",  # Country-style bread, sourdough/French
    "175042",  # Fresh baker's yeast
    "172238",  # Capers, canned
    "170080",  # Jalapeno peppers, canned
    "170932",  # Cayenne pepper
    "170933",  # White pepper
    "171333",  # Rosemary
    "170938",  # Thyme
    "171317",  # Basil
    "170928",  # Marjoram
    "171328",  # Oregano
    "172231",  # Turmeric
    "170923",  # Cumin seed
    "170926",  # Ginger, ground
    "171320",  # Cinnamon, ground
    "171326",  # Nutmeg, ground
    "170918",  # Caraway seed
    "171329",  # Paprika
    "170934",  # Saffron
    "170922",  # Coriander seed
    "170919",  # Cardamom
    "171323",  # Fennel seed
    "173756",  # Chickpeas, raw
    "175202",  # White beans, raw
    "170419",  # Green peas, raw
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
    "167746": ("Llimona", "Llimona", "Limón"),
    "167747": ("Suc de llimona", "Suc de llimona", "Zumo de limón"),
    "168462": ("Espinacs crus", "Espinacs crus", "Espinacas crudas"),
    "169705": ("Civada", "Civada", "Avena"),
    "169094": ("Olives negres en conserva", "Olives negres en conserva", "Aceitunas negras en conserva"),
    "169096": ("Olives verdes en conserva", "Olives verdes en conserva", "Aceitunas verdes en conserva"),
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
    "171304": ("Iogurt grec sencer natural", "Iogurt grec sencer natural", "Yogur griego entero natural"),
    "171265": ("Llet sencera", "Llet sencera", "Leche entera"),
    "170857": ("Crema de llet", "Crema de llet", "Nata ligera"),
    "173410": ("Mantega amb sal", "Mantega amb sal", "Mantequilla con sal"),
    "173430": ("Mantega sense sal", "Mantega sense sal", "Mantequilla sin sal"),
    "171287": ("Ou fresc cru", "Ou fresc cru", "Huevo fresco crudo"),
    "170848": ("Formatge parmesà bloc", "Formatge parmesà bloc", "Queso parmesano en bloque"),
    "171247": ("Formatge parmesà ratllat", "Formatge parmesà ratllat", "Queso parmesano rallado"),
    "173431": ("Formatge parmesà en fils", "Formatge parmesà en fils", "Queso parmesano rallado grueso"),
    "170845": ("Formatge mozzarella bola", "Formatge mozzarella bola", "Queso mozzarella fresco"),
    "170900": ("Formatge mozzarella ratllat", "Formatge mozzarella ratllat", "Queso mozzarella rallado"),
    "171251": ("Formatge emmental bloc", "Formatge emmental bloc", "Queso emmental en bloque"),
    "173414": ("Formatge cheddar bloc", "Formatge cheddar bloc", "Queso cheddar en bloque"),
    "170899": ("Formatge cheddar llesques", "Formatge cheddar llesques", "Queso cheddar en lonchas"),
    "171241": ("Formatge gouda bloc", "Formatge gouda bloc", "Queso gouda en bloque"),
    "173420": ("Formatge feta bloc", "Formatge feta bloc", "Queso feta en bloque"),
    "172175": ("Formatge blau", "Formatge blau", "Queso azul"),
    "173418": ("Formatge crema", "Formatge crema", "Queso crema"),
    "173435": ("Formatge rulo de cabra", "Formatge rulo de cabra", "Queso de cabra blando"),
    "173433": ("Formatge de cabra semicurat", "Formatge de cabra semicurat", "Queso de cabra semicurado"),
    "172197": ("Formatge de cabra curat", "Formatge de cabra curat", "Queso de cabra curado"),
    "170851": ("Formatge ricotta sencer", "Formatge ricotta sencer", "Queso ricotta entero"),
    "168000": ("Crema de cacau i avellanes", "Crema de cacau i avellanes", "Crema de cacao y avellanas"),
    "170273": ("Xocolata negra 70-85%", "Xocolata negra 70-85%", "Chocolate negro 70-85%"),
    "174183": ("Anxoves en conserva amb oli", "Anxoves en conserva amb oli", "Anchoas en conserva en aceite"),
    "173708": ("Tonyina en conserva amb oli", "Tonyina en conserva amb oli", "Atun en conserva en aceite"),
    "174190": ("Bacallà salat", "Bacallà salat", "Bacalao salado"),
    "173687": ("Salmó fumat", "Salmó fumat", "Salmon ahumado"),
    "175119": ("Verat fresc", "Verat fresc", "Caballa fresca"),
    "174215": ("Sípia fresca", "Sípia fresca", "Sepia fresca"),
    "174214": ("Cloïsses fresques", "Cloïsses fresques", "Almejas frescas"),
    "171509": ("Pit de pollastre cru", "Pit de pollastre cru", "Pechuga de pollo cruda"),
    "172378": ("Quart de darrere de pollastre cru", "Quart de darrere de pollastre cru", "Muslo de pollo crudo"),
    "172876": ("Pit de gall dindi cru", "Pit de gall dindi cru", "Pechuga de pavo cruda"),
    "172941": ("Pit de gall dindi embotit", "Pit de gall dindi embotit", "Pechuga de pavo loncheada"),
    "172408": ("Ànec cru amb pell", "Ànec cru amb pell", "Pato crudo con piel"),
    "168312": ("Filet de porc cru", "Filet de porc cru", "Solomillo de cerdo crudo"),
    "168263": ("Cinta de porc crua", "Cinta de porc crua", "Lomo de cerdo crudo"),
    "168295": ("Pernil curat", "Pernil curat", "Jamón curado"),
    "173859": ("Xoriço cru", "Xoriço cru", "Chorizo crudo"),
    "174603": ("Salami italià", "Salami italià", "Salami italiano"),
    "169542": ("Filet de vedella cru", "Filet de vedella cru", "Solomillo de ternera crudo"),
    "168693": ("Espatlla de vedella crua", "Espatlla de vedella crua", "Espalda de ternera cruda"),
    "169928": ("Préssec groc cru", "Préssec groc cru", "Melocoton amarillo crudo"),
    "169914": ("Nectarina crua", "Nectarina crua", "Nectarina cruda"),
    "174683": ("Raïm sense llavors cru", "Raïm sense llavors cru", "Uva sin semillas cruda"),
    "167793": ("Poma Fuji crua amb pell", "Poma Fuji crua amb pell", "Manzana Fuji cruda con piel"),
    "169911": ("Meló de carn blanca", "Meló de carn blanca", "Melon piel de sapo aproximado"),
    "167765": ("Síndria crua", "Síndria crua", "Sandia cruda"),
    "169097": ("Taronja crua", "Taronja crua", "Naranja cruda"),
    "169105": ("Mandarina crua", "Mandarina crua", "Mandarina cruda"),
    "168195": ("Clementina crua", "Clementina crua", "Clementina cruda"),
    "171705": ("Alvocat cru", "Alvocat cru", "Aguacate crudo"),
    "169988": ("Api cru", "Api cru", "Apio crudo"),
    "170000": ("Ceba crua", "Ceba crua", "Cebolla cruda"),
    "170008": ("Ceba dolça crua", "Ceba dolça crua", "Cebolla dulce cruda"),
    "170005": ("Ceba tendra crua", "Ceba tendra crua", "Cebolleta cruda"),
    "169230": ("All sec cru", "All sec cru", "Ajo seco crudo"),
    "170393": ("Pastanaga crua", "Pastanaga crua", "Zanahoria cruda"),
    "170379": ("Bròcoli cru", "Bròcoli cru", "Brocoli crudo"),
    "169291": ("Carbassó cru", "Carbassó cru", "Calabacin crudo"),
    "169228": ("Albergínia crua", "Albergínia crua", "Berenjena cruda"),
    "170457": ("Tomàquet cru", "Tomàquet cru", "Tomate crudo"),
    "168429": ("Enciam trocadero", "Enciam trocadero", "Lechuga mantecosa"),
    "169249": ("Enciam fulla verda", "Enciam fulla verda", "Lechuga hoja verde"),
    "168431": ("Enciam fulla de roure", "Enciam fulla de roure", "Lechuga hoja roja"),
    "170427": ("Pebrot verd cru", "Pebrot verd cru", "Pimiento verde crudo"),
    "170108": ("Pebrot vermell cru", "Pebrot vermell cru", "Pimiento rojo crudo"),
    "169756": ("Arròs basmati cru", "Arròs basmati cru", "Arroz basmati crudo"),
    "168931": ("Arròs bomba cru", "Arròs bomba cru", "Arroz redondo crudo"),
    "168927": ("Pasta seca", "Pasta seca", "Pasta seca"),
    "169731": ("Noodles d'ou secs", "Noodles d'ou secs", "Fideos de huevo secos"),
    "169742": ("Noodles d'arròs secs", "Noodles d'arròs secs", "Fideos de arroz secos"),
    "169727": ("Pasta fresca", "Pasta fresca", "Pasta fresca"),
    "167535": ("Wrap de farina de blat", "Wrap de farina de blat", "Tortilla de harina de trigo"),
    "168936": ("Farina de blat tot ús", "Farina de blat tot ús", "Harina de trigo todo uso"),
    "168896": ("Farina de força", "Farina de força", "Harina de fuerza"),
    "168893": ("Farina integral de blat", "Farina integral de blat", "Harina integral de trigo"),
    "169745": ("Espelta integral", "Espelta integral", "Espelta integral"),
    "169695": ("Farina de blat de moro groga", "Farina de blat de moro groga", "Harina de maiz amarilla"),
    "169697": ("Farina de blat de moro integral", "Farina de blat de moro integral", "Harina integral de maiz"),
    "168929": ("Polenta", "Polenta", "Polenta"),
    "170187": ("Nous", "Nous", "Nueces"),
    "170567": ("Ametlles", "Ametlles", "Almendras"),
    "170556": ("Pipes de carbassa", "Pipes de carbassa", "Pipas de calabaza"),
    "170150": ("Sèsam", "Sèsam", "Sesamo"),
    "168874": ("Quinoa crua", "Quinoa crua", "Quinoa cruda"),
    "170554": ("Chia", "Chia", "Chia"),
    "169414": ("Lli marró", "Lli marró", "Lino marron"),
    "171401": ("Llard de porc", "Llard de porc", "Manteca de cerdo"),
    "171017": ("Oli de gira-sol", "Oli de gira-sol", "Aceite de girasol"),
    "172241": ("Vinagre de Mòdena", "Vinagre de Mòdena", "Vinagre balsamico"),
    "172240": ("Vinagre de vi", "Vinagre de vi", "Vinagre de vino"),
    "173469": ("Vinagre de poma", "Vinagre de poma", "Vinagre de manzana"),
    "171610": ("Salsa Worcestershire", "Salsa Worcestershire", "Salsa Worcestershire"),
    "172234": ("Mostassa preparada", "Mostassa preparada", "Mostaza preparada"),
    "168556": ("Ketchup", "Ketchup", "Ketchup"),
    "171186": ("Sriracha", "Sriracha", "Sriracha"),
    "168746": ("Cervesa", "Cervesa", "Cerveza"),
    "171890": ("Cafè preparat", "Cafè preparat", "Cafe preparado"),
    "169655": ("Sucre blanc", "Sucre blanc", "Azucar blanco"),
    "168833": ("Sucre morè", "Sucre morè", "Azucar moreno"),
    "170674": ("Panela", "Panela", "Panela"),
    "169409": ("Llet de coco", "Llet de coco", "Leche de coco"),
    "171711": ("Nabius crus", "Nabius crus", "Arandanos crudos"),
    "167762": ("Maduixes crues", "Maduixes crues", "Fresas crudas"),
    "167755": ("Gerds crus", "Gerds crus", "Frambuesas crudas"),
    "173946": ("Mores crues", "Mores crues", "Moras crudas"),
    "170645": ("Melmelada d'albercoc", "Melmelada d'albercoc", "Mermelada de albaricoque"),
    "169641": ("Melmelada de fruita", "Melmelada de fruita", "Mermelada de fruta"),
    "172688": ("Pa de motlle integral", "Pa de motlle integral", "Pan de molde integral"),
    "174924": ("Pa blanc comercial", "Pa blanc comercial", "Pan blanco comercial"),
    "172675": ("Pa de pagès", "Pa de pagès", "Pan de payes"),
    "175042": ("Llevat fresc de forner", "Llevat fresc de forner", "Levadura fresca de panadero"),
    "172238": ("Tàperes en conserva", "Tàperes en conserva", "Alcaparras en conserva"),
    "170080": ("Jalapeños en rodanxes", "Jalapeños en rodanxes", "Jalapenos en rodajas"),
    "170932": ("Pebre vermell picant", "Pebre vermell picant", "Pimienta cayena"),
    "170933": ("Pebre blanc", "Pebre blanc", "Pimienta blanca"),
    "171333": ("Romaní sec", "Romaní sec", "Romero seco"),
    "170938": ("Farigola seca", "Farigola seca", "Tomillo seco"),
    "171317": ("Alfàbrega seca", "Alfàbrega seca", "Albahaca seca"),
    "170928": ("Marduix sec", "Marduix sec", "Mejorana seca"),
    "171328": ("Orenga seca", "Orenga seca", "Oregano seco"),
    "172231": ("Cúrcuma molta", "Cúrcuma molta", "Curcuma molida"),
    "170923": ("Comí en gra", "Comí en gra", "Comino en grano"),
    "170926": ("Gingebre molt", "Gingebre molt", "Jengibre molido"),
    "171320": ("Canyella molta", "Canyella molta", "Canela molida"),
    "171326": ("Nou moscada molta", "Nou moscada molta", "Nuez moscada molida"),
    "170918": ("Alcaravea en gra", "Alcaravea en gra", "Alcaravea en grano"),
    "171329": ("Pimentón de la Vera", "Pimentón de la Vera", "Pimenton de la Vera"),
    "170934": ("Safrà", "Safrà", "Azafran"),
    "170922": ("Coriandre en gra", "Coriandre en gra", "Cilantro en grano"),
    "170919": ("Cardamom", "Cardamom", "Cardamomo"),
    "171323": ("Fonoll en gra", "Fonoll en gra", "Hinojo en grano"),
    "173756": ("Cigrons", "Cigrons", "Garbanzos"),
    "175202": ("Mongetes blanques", "Mongetes blanques", "Alubias blancas"),
    "170419": ("Pèsols crus", "Pèsols crus", "Guisantes crudos"),
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
    "170170": ["coco", "coco ratllat"],
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
    "167746": ["llimona", "limona", "limón", "limon", "pell de llimona"],
    "167747": ["suc de llimona", "zumo de limon", "zumo de limón", "lemon juice"],
    "168462": ["espinacs", "espinacas", "spinach"],
    "169705": ["civada", "avena", "flocs de civada", "copos de avena"],
    "169094": ["olives negres", "aceitunas negras", "black olives"],
    "169096": ["olives verdes", "aceitunas verdes", "green olives", "olives verdes farcides", "olives verdes farcides d'anxova", "aceitunas rellenas de anchoa"],
    "167818": ["llom porc", "lomo cerdo"],
    "170859": ["nata muntar", "nata liquida", "heavy cream"],
    "171412": ["aceite de coco"],
    "169998": ["maiz", "corn"],
    "169640": ["miel"],
    "174223": ["squid"],
    "174216": ["musclo", "mejillon"],
    "171316": ["anis", "anís", "matafaluga"],
    "171304": ["iogurt grec", "yogur griego"],
    "171265": ["llet Castillo", "leche entera"],
    "170857": ["crema de llet ATO", "nata de cocinar"],
    "173410": ["mantega salada", "mantega amb sal Harmony"],
    "173430": ["mantega sense sal Central Lechera Asturiana"],
    "171287": ["ous eco", "ous", "dotzena d'ous", "huevos"],
    "170848": ["parmesano bloc", "parmesa bloc"],
    "171247": ["parmesano ratllat", "parmesa ratllat"],
    "173431": ["parmesano en fils"],
    "170845": ["mozzarella bola", "mozzarella fresca"],
    "170900": ["mozzarella ratllada", "mozzarella ratlla"],
    "171251": ["emmental", "emmental bloc"],
    "173414": ["cheddar", "cheddar bloc"],
    "170899": ["cheddar llenques", "cheddar lonchas"],
    "171241": ["gouda", "barreja de formatges ratllats"],
    "173420": ["formatge fera", "feta"],
    "173418": ["Philadelphia"],
    "173435": ["rulo de cabra", "formatge de cabra llenques"],
    "173433": ["formatge de cabra ovella llenques"],
    "168000": ["nocilla", "crema d'avellanes"],
    "170273": ["xocolata Valor 82%", "xocolata 82%", "xocolata amb ametlles"],
    "174183": ["anxoves oli oliva"],
    "173708": ["tonyina Consorcio", "tonyina oli oliva"],
    "174190": ["bacallà dessalat", "bacallà salat i dessalat"],
    "173687": ["salmó fumat"],
    "175119": ["caballa"],
    "174215": ["sepia", "sípia"],
    "174214": ["cloïses"],
    "171509": ["pit de pollastre"],
    "172378": ["quarts de darrera de pollastre"],
    "172876": ["pit de gall dindi"],
    "172941": ["pit de pavo", "pit de gall dindi embotit", "pechuga de pavo", "pavo loncheado"],
    "172408": ["magret d'ànec"],
    "168312": ["filet de porc", "solomillo de cerdo"],
    "168263": ["cinta de porc"],
    "168295": ["pernil salat", "jamon serrano", "jamón serrano", "espatlla iberica", "espatlla ibèrica"],
    "173859": ["xoriço", "chorizo"],
    "174603": ["salami"],
    "169542": ["filet de vedella"],
    "168693": ["espatlla de vedella"],
    "169928": ["préssec de vinya", "préssec d'aigua"],
    "169914": ["nectarina"],
    "174683": ["raïm sense llavors"],
    "167793": ["poma fuji"],
    "169911": ["meló pell de gripau", "meló pell arrugada"],
    "169105": ["mandarina", "clemenvilla"],
    "168195": ["clementina"],
    "171705": ["alvocat"],
    "170000": ["ceba blanca"],
    "170008": ["ceba figueres"],
    "170005": ["ceba tendre", "alls tendres"],
    "169230": ["alls secs"],
    "170457": ["tomàquet xerri", "tomàquet cor de bou", "tomate cherry"],
    "168429": ["enciam trocadero"],
    "168431": ["enciam fulla de roure"],
    "169756": ["arròs basmati"],
    "168931": ["arròs bomba"],
    "168927": ["macarrons", "espaguetis", "parpadelle", "fetuccini"],
    "169731": ["noodles"],
    "167535": ["wraps durum", "wrap dürum"],
    "168936": ["farina tot ús", "farina blat tot ús"],
    "168896": ["farina força"],
    "168893": ["farina integral"],
    "169745": ["farina integral d'espelta", "espelta"],
    "169695": ["farina blat de moro", "Doñana amarilla"],
    "168929": ["polenta"],
    "170187": ["anous", "nous"],
    "170567": ["ametlles"],
    "170556": ["pipes de carbassa"],
    "170150": ["sesam", "sèsam"],
    "169414": ["lli marro", "lli marró"],
    "171017": ["oli oliva suau 0.4", "oli girasol"],
    "172241": ["vinagre modena", "vinagre de modena"],
    "171610": ["salsa lea perrins", "salsa worcester"],
    "172234": ["mostassa antiga dijon", "mostassa dijon"],
    "171186": ["pasta sriracha"],
    "168746": ["cervesa per cuinar"],
    "171890": ["cafe", "cafè"],
    "170674": ["sucre panela"],
    "171711": ["nabius", "fruits vermells"],
    "167762": ["maduixes", "fruits vermells"],
    "167755": ["gerds", "fruits vermells"],
    "173946": ["mores", "fruits vermells"],
    "170645": ["mermelada albercoc"],
    "169641": ["mermelada maduixa"],
    "172688": ["pa de motlle oroweat integral amb llavors", "pa de motlle integral amb llavors", "oroweat etiqueta negra"],
    "174924": ["pa de motlle blanc", "pa ratllat", "pan rallado"],
    "175042": ["llevat fresc panader", "llevat fresc de panader"],
    "169599": ["cua de peix", "gelificant", "gelatina neutra"],
    "172238": ["taperes", "tàperes"],
    "170080": ["jalapenos rodanxes", "jalapeños en rodanxes"],
    "171333": ["romani", "romaní"],
    "170938": ["farigola"],
    "171317": ["herbes provençals"],
    "170928": ["herbes provençals"],
    "171328": ["herbes provençals"],
    "170924": ["curry", "curri en pols", "garam masala", "curry tandoori", "curry madràs", "ras-al-hanut", "ras el hanout"],
    "172231": ["curcuma", "cúrcuma"],
    "170923": ["comi", "comí"],
    "170926": ["gingebre sec", "gingebre en pols", "jengibre molido"],
    "171320": ["canyella en branca", "canela en rama"],
    "171326": ["nou moscada en gra", "nou moscada molta"],
    "170918": ["alcaravea"],
    "171329": ["pimenton", "pimentón", "paprika"],
    "170932": ["guindilla cayena", "cayena sencera", "cayena molta"],
    "173756": ["cigrons de Salamanca", "garbanzos"],
    "175202": ["mongetes del ganxet", "mongetes"],
    "170419": ["pèsols del Maresme", "pèsols"],
}

BLOCKED_ALIASES_BY_FDC_ID = {
    "173859": [
        "botifarra",
        "botifarra blanca",
        "botifarra negra",
        "butifarra",
        "costella de porc",
        "costilla",
        "fuet",
        "llonganissa",
        "longaniza",
        "salami",
        "salchicha",
    ],
    "174603": [
        "botifarra",
        "botifarra blanca",
        "botifarra negra",
        "butifarra",
        "chorizo",
        "costella de porc",
        "costilla",
        "fuet",
        "llonganissa",
        "longaniza",
        "xoriço",
    ],
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


def should_exclude_food(description, category):
    n = norm(description)
    if contains_any(description, EXCLUDE_PATTERNS):
        return True

    if category == "Dairy and Egg Products":
        blocked = [
            "salad dressing",
            "ice cream",
            "frozen yogurt",
            "frosting",
            "cheese substitute",
            "yogurt chocolate",
            "yogurt frozen",
            "egg yolk raw frozen sugared",
            "milk chocolate",
            "hot cocoa",
            "buttermilk",
        ]
        return any(token in n for token in blocked)

    if category == "Pork Products" and "fresh leg ham" in n:
        return True

    return False


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
    dairy_name = dairy_localized_name(n)
    if dairy_name is not None:
        return dairy_name

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


def dairy_localized_name(n):
    if "cheese" in n:
        variants = [
            ("parmesan", "Formatge parmesà", "Queso parmesano"),
            ("mozzarella", "Formatge mozzarella", "Queso mozzarella"),
            ("cheddar", "Formatge cheddar", "Queso cheddar"),
            ("gouda", "Formatge gouda", "Queso gouda"),
            ("swiss", "Formatge emmental", "Queso emmental"),
            ("feta", "Formatge feta", "Queso feta"),
            ("blue", "Formatge blau", "Queso azul"),
            ("cream", "Formatge crema", "Queso crema"),
            ("goat", "Formatge de cabra", "Queso de cabra"),
            ("ricotta", "Formatge ricotta", "Queso ricotta"),
            ("cottage", "Formatge cottage", "Queso cottage"),
        ]
        base_ca, base_es = "Formatge", "Queso"
        for token, ca, es in variants:
            if token in n:
                base_ca, base_es = ca, es
                break

        descriptors = []
        descriptors_es = []
        if "grated" in n or "shredded" in n:
            descriptors.append("ratllat")
            descriptors_es.append("rallado")
        if "sliced" in n:
            descriptors.append("llesques")
            descriptors_es.append("lonchas")
        if "whole milk" in n:
            descriptors.append("llet sencera")
            descriptors_es.append("leche entera")
        if "part skim" in n:
            descriptors.append("semi")
            descriptors_es.append("semidesnatado")
        if "low sodium" in n:
            descriptors.append("baix en sal")
            descriptors_es.append("bajo en sal")

        ca_name = " ".join([base_ca, *descriptors]).strip()
        es_name = " ".join([base_es, *descriptors_es]).strip()
        return ca_name, ca_name, es_name

    if "yogurt" in n:
        base_ca = "Iogurt grec" if "greek" in n else "Iogurt"
        base_es = "Yogur griego" if "greek" in n else "Yogur"
        descriptors = []
        descriptors_es = []
        if "plain" in n:
            descriptors.append("natural")
            descriptors_es.append("natural")
        if "whole milk" in n:
            descriptors.append("sencer")
            descriptors_es.append("entero")
        elif "lowfat" in n or "low fat" in n:
            descriptors.append("baix en greix")
            descriptors_es.append("bajo en grasa")
        elif "nonfat" in n or "skim" in n:
            descriptors.append("desnatat")
            descriptors_es.append("desnatado")
        ca_name = " ".join([base_ca, *descriptors]).strip()
        es_name = " ".join([base_es, *descriptors_es]).strip()
        return ca_name, ca_name, es_name

    if "butter" in n and "buttermilk" not in n:
        if "without salt" in n or "unsalted" in n:
            return "Mantega sense sal", "Mantega sense sal", "Mantequilla sin sal"
        if "salted" in n:
            return "Mantega amb sal", "Mantega amb sal", "Mantequilla con sal"
        return "Mantega", "Mantega", "Mantequilla"

    if "egg" in n:
        if "whole" in n and "raw fresh" in n:
            return "Ou fresc cru", "Ou fresc cru", "Huevo fresco crudo"
        if "hard boiled" in n:
            return "Ou dur", "Ou dur", "Huevo duro"
        if "poached" in n:
            return "Ou poché", "Ou poché", "Huevo poche"
        if "fried" in n:
            return "Ou ferrat", "Ou ferrat", "Huevo frito"
        if "scrambled" in n:
            return "Ou remenat", "Ou remenat", "Huevo revuelto"
        return "Ou", "Ou", "Huevo"

    return None


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


def human_suffix(description):
    n = norm(description)
    suffixes = [
        ("lowfat", "baix greix", "bajo grasa"),
        ("low fat", "baix greix", "bajo grasa"),
        ("nonfat", "desnatat", "desnatado"),
        ("fat free", "desnatat", "desnatado"),
        ("whole milk", "llet sencera", "leche entera"),
        ("part skim", "semi", "semi"),
        ("low moisture", "baixa humitat", "baja humedad"),
        ("low sodium", "baix sal", "bajo sal"),
        ("grated", "ratllat", "rallado"),
        ("shredded", "en fils", "rallado grueso"),
        ("sliced", "llesques", "lonchas"),
        ("hard type", "curat", "curado"),
        ("semisoft", "semicurat", "semicurado"),
        ("soft type", "tou", "blando"),
        ("raw", "cru", "crudo"),
        ("cooked", "cuit", "cocido"),
        ("dry", "sec", "seco"),
        ("dried", "sec", "seco"),
        ("canned", "conserva", "conserva"),
    ]
    for token, ca, es in suffixes:
        if token in n:
            return ca, es
    return "", ""


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
        if should_exclude_food(row["description"], category):
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
            suffix_ca, suffix_es = human_suffix(row["description"])
            if suffix_ca and suffix_ca not in norm(canonical):
                canonical = f"{canonical} {suffix_ca}"
                catalan = f"{catalan} {suffix_ca}"
                spanish = f"{spanish} {suffix_es}"
            else:
                suffix = seen_names[canonical]
                canonical = f"{canonical} variant {suffix}"
                catalan = f"{catalan} variant {suffix}"
                spanish = f"{spanish} variante {suffix}"

        nutrition = nutrient_by_food[row["fdc_id"]]
        sodium = nutrition.get("sodium")
        aliases = {
            row["description"],
            *localized_base_names(row["description"]),
            *EXTRA_ALIASES_BY_FDC_ID.get(row["fdc_id"], []),
        }
        for alias in list(aliases):
            aliases.update(household_aliases.get(norm(alias), set()))
        blocked_aliases = {
            norm(alias)
            for alias in BLOCKED_ALIASES_BY_FDC_ID.get(row["fdc_id"], [])
        }
        if blocked_aliases:
            aliases = {alias for alias in aliases if norm(alias) not in blocked_aliases}
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
