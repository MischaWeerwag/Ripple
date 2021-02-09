using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Ibasa.Ripple
{
    /// <summary>
    /// https://tools.ietf.org/html/rfc1751
    /// </summary>
    public static class Rfc1751
    {
        private static string[] words = new string [] {
            "A", "ABE", "ACE", "ACT", "AD", "ADA", "ADD", "AGO", "AID", "AIM", "AIR", "ALL", "ALP", "AM", "AMY", "AN", "ANA",
            "AND", "ANN", "ANT", "ANY", "APE", "APS", "APT", "ARC", "ARE", "ARK", "ARM", "ART", "AS", "ASH", "ASK", "AT", "ATE", "AUG", "AUK", "AVE",
            "AWE", "AWK", "AWL", "AWN", "AX", "AYE", "BAD", "BAG", "BAH", "BAM", "BAN", "BAR", "BAT", "BAY", "BE", "BED", "BEE", "BEG", "BEN", "BET",
            "BEY", "BIB", "BID", "BIG", "BIN", "BIT", "BOB", "BOG", "BON", "BOO", "BOP", "BOW", "BOY", "BUB", "BUD", "BUG", "BUM", "BUN", "BUS", "BUT",
            "BUY", "BY", "BYE", "CAB", "CAL", "CAM", "CAN", "CAP", "CAR", "CAT", "CAW", "COD", "COG", "COL", "CON", "COO", "COP", "COT", "COW", "COY",
            "CRY", "CUB", "CUE", "CUP", "CUR", "CUT", "DAB", "DAD", "DAM", "DAN", "DAR", "DAY", "DEE", "DEL", "DEN", "DES", "DEW", "DID", "DIE", "DIG",
            "DIN", "DIP", "DO", "DOE", "DOG", "DON", "DOT", "DOW", "DRY", "DUB", "DUD", "DUE", "DUG", "DUN", "EAR", "EAT", "ED", "EEL", "EGG", "EGO",
            "ELI", "ELK", "ELM", "ELY", "EM", "END", "EST", "ETC", "EVA", "EVE", "EWE", "EYE", "FAD", "FAN", "FAR", "FAT", "FAY", "FED", "FEE", "FEW",
            "FIB", "FIG", "FIN", "FIR", "FIT", "FLO", "FLY", "FOE", "FOG", "FOR", "FRY", "FUM", "FUN", "FUR", "GAB", "GAD", "GAG", "GAL", "GAM", "GAP",
            "GAS", "GAY", "GEE", "GEL", "GEM", "GET", "GIG", "GIL", "GIN", "GO", "GOT", "GUM", "GUN", "GUS", "GUT", "GUY", "GYM", "GYP", "HA", "HAD",
            "HAL", "HAM", "HAN", "HAP", "HAS", "HAT", "HAW", "HAY", "HE", "HEM", "HEN", "HER", "HEW", "HEY", "HI", "HID", "HIM", "HIP", "HIS", "HIT",
            "HO", "HOB", "HOC", "HOE", "HOG", "HOP", "HOT", "HOW", "HUB", "HUE", "HUG", "HUH", "HUM", "HUT", "I", "ICY", "IDA", "IF", "IKE", "ILL",
            "INK", "INN", "IO", "ION", "IQ", "IRA", "IRE", "IRK", "IS", "IT", "ITS", "IVY", "JAB", "JAG", "JAM", "JAN", "JAR", "JAW", "JAY", "JET", "JIG",
            "JIM", "JO", "JOB", "JOE", "JOG", "JOT", "JOY", "JUG", "JUT", "KAY", "KEG", "KEN", "KEY", "KID", "KIM", "KIN", "KIT", "LA", "LAB", "LAC",
            "LAD", "LAG", "LAM", "LAP", "LAW", "LAY", "LEA", "LED", "LEE", "LEG", "LEN", "LEO", "LET", "LEW", "LID", "LIE", "LIN", "LIP", "LIT", "LO",
            "LOB", "LOG", "LOP", "LOS", "LOT", "LOU", "LOW", "LOY", "LUG", "LYE", "MA", "MAC", "MAD", "MAE", "MAN", "MAO", "MAP", "MAT", "MAW", "MAY",
            "ME", "MEG", "MEL", "MEN", "MET", "MEW", "MID", "MIN", "MIT", "MOB", "MOD", "MOE", "MOO", "MOP", "MOS", "MOT", "MOW", "MUD", "MUG", "MUM",
            "MY", "NAB", "NAG", "NAN", "NAP", "NAT", "NAY", "NE", "NED", "NEE", "NET", "NEW", "NIB", "NIL", "NIP", "NIT", "NO", "NOB", "NOD", "NON",
            "NOR", "NOT", "NOV", "NOW", "NU", "NUN", "NUT", "O", "OAF", "OAK", "OAR", "OAT", "ODD", "ODE", "OF", "OFF", "OFT", "OH", "OIL", "OK",
            "OLD", "ON", "ONE", "OR", "ORB", "ORE", "ORR", "OS", "OTT", "OUR", "OUT", "OVA", "OW", "OWE", "OWL", "OWN", "OX", "PA", "PAD", "PAL",
            "PAM", "PAN", "PAP", "PAR", "PAT", "PAW", "PAY", "PEA", "PEG", "PEN", "PEP", "PER", "PET", "PEW", "PHI", "PI", "PIE", "PIN", "PIT", "PLY",
            "PO", "POD", "POE", "POP", "POT", "POW", "PRO", "PRY", "PUB", "PUG", "PUN", "PUP", "PUT", "QUO", "RAG", "RAM", "RAN", "RAP", "RAT", "RAW",
            "RAY", "REB", "RED", "REP", "RET", "RIB", "RID", "RIG", "RIM", "RIO", "RIP", "ROB", "ROD", "ROE", "RON", "ROT", "ROW", "ROY", "RUB", "RUE",
            "RUG", "RUM", "RUN", "RYE", "SAC", "SAD", "SAG", "SAL", "SAM", "SAN", "SAP", "SAT", "SAW", "SAY", "SEA", "SEC", "SEE", "SEN", "SET", "SEW",
            "SHE", "SHY", "SIN", "SIP", "SIR", "SIS", "SIT", "SKI", "SKY", "SLY", "SO", "SOB", "SOD", "SON", "SOP", "SOW", "SOY", "SPA", "SPY", "SUB",
            "SUD", "SUE", "SUM", "SUN", "SUP", "TAB", "TAD", "TAG", "TAN", "TAP", "TAR", "TEA", "TED", "TEE", "TEN", "THE", "THY", "TIC", "TIE", "TIM",
            "TIN", "TIP", "TO", "TOE", "TOG", "TOM", "TON", "TOO", "TOP", "TOW", "TOY", "TRY", "TUB", "TUG", "TUM", "TUN", "TWO", "UN", "UP", "US",
            "USE", "VAN", "VAT", "VET", "VIE", "WAD", "WAG", "WAR", "WAS", "WAY", "WE", "WEB", "WED", "WEE", "WET", "WHO", "WHY", "WIN", "WIT", "WOK",
            "WON", "WOO", "WOW", "WRY", "WU", "YAM", "YAP", "YAW", "YE", "YEA", "YES", "YET", "YOU", "ABED", "ABEL", "ABET", "ABLE", "ABUT", "ACHE",
            "ACID", "ACME", "ACRE", "ACTA", "ACTS", "ADAM", "ADDS", "ADEN", "AFAR", "AFRO", "AGEE", "AHEM", "AHOY", "AIDA", "AIDE", "AIDS", "AIRY", "AJAR",
            "AKIN", "ALAN", "ALEC", "ALGA", "ALIA", "ALLY", "ALMA", "ALOE", "ALSO", "ALTO", "ALUM", "ALVA", "AMEN", "AMES", "AMID", "AMMO", "AMOK", "AMOS",
            "AMRA", "ANDY", "ANEW", "ANNA", "ANNE", "ANTE", "ANTI", "AQUA", "ARAB", "ARCH", "AREA", "ARGO", "ARID", "ARMY", "ARTS", "ARTY", "ASIA", "ASKS",
            "ATOM", "AUNT", "AURA", "AUTO", "AVER", "AVID", "AVIS", "AVON", "AVOW", "AWAY", "AWRY", "BABE", "BABY", "BACH", "BACK", "BADE", "BAIL", "BAIT",
            "BAKE", "BALD", "BALE", "BALI", "BALK", "BALL", "BALM", "BAND", "BANE", "BANG", "BANK", "BARB", "BARD", "BARE", "BARK", "BARN", "BARR", "BASE",
            "BASH", "BASK", "BASS", "BATE", "BATH", "BAWD", "BAWL", "BEAD", "BEAK", "BEAM", "BEAN", "BEAR", "BEAT", "BEAU", "BECK", "BEEF", "BEEN", "BEER",
            "BEET", "BELA", "BELL", "BELT", "BEND", "BENT", "BERG", "BERN", "BERT", "BESS", "BEST", "BETA", "BETH", "BHOY", "BIAS", "BIDE", "BIEN", "BILE",
            "BILK", "BILL", "BIND", "BING", "BIRD", "BITE", "BITS", "BLAB", "BLAT", "BLED", "BLEW", "BLOB", "BLOC", "BLOT", "BLOW", "BLUE", "BLUM", "BLUR",
            "BOAR", "BOAT", "BOCA", "BOCK", "BODE", "BODY", "BOGY", "BOHR", "BOIL", "BOLD", "BOLO", "BOLT", "BOMB", "BONA", "BOND", "BONE", "BONG", "BONN",
            "BONY", "BOOK", "BOOM", "BOON", "BOOT", "BORE", "BORG", "BORN", "BOSE", "BOSS", "BOTH", "BOUT", "BOWL", "BOYD", "BRAD", "BRAE", "BRAG", "BRAN",
            "BRAY", "BRED", "BREW", "BRIG", "BRIM", "BROW", "BUCK", "BUDD", "BUFF", "BULB", "BULK", "BULL", "BUNK", "BUNT", "BUOY", "BURG", "BURL", "BURN",
            "BURR", "BURT", "BURY", "BUSH", "BUSS", "BUST", "BUSY", "BYTE", "CADY", "CAFE", "CAGE", "CAIN", "CAKE", "CALF", "CALL", "CALM", "CAME", "CANE",
            "CANT", "CARD", "CARE", "CARL", "CARR", "CART", "CASE", "CASH", "CASK", "CAST", "CAVE", "CEIL", "CELL", "CENT", "CERN", "CHAD", "CHAR", "CHAT",
            "CHAW", "CHEF", "CHEN", "CHEW", "CHIC", "CHIN", "CHOU", "CHOW", "CHUB", "CHUG", "CHUM", "CITE", "CITY", "CLAD", "CLAM", "CLAN", "CLAW", "CLAY",
            "CLOD", "CLOG", "CLOT", "CLUB", "CLUE", "COAL", "COAT", "COCA", "COCK", "COCO", "CODA", "CODE", "CODY", "COED", "COIL", "COIN", "COKE", "COLA",
            "COLD", "COLT", "COMA", "COMB", "COME", "COOK", "COOL", "COON", "COOT", "CORD", "CORE", "CORK", "CORN", "COST", "COVE", "COWL", "CRAB", "CRAG",
            "CRAM", "CRAY", "CREW", "CRIB", "CROW", "CRUD", "CUBA", "CUBE", "CUFF", "CULL", "CULT", "CUNY", "CURB", "CURD", "CURE", "CURL", "CURT", "CUTS",
            "DADE", "DALE", "DAME", "DANA", "DANE", "DANG", "DANK", "DARE", "DARK", "DARN", "DART", "DASH", "DATA", "DATE", "DAVE", "DAVY", "DAWN", "DAYS",
            "DEAD", "DEAF", "DEAL", "DEAN", "DEAR", "DEBT", "DECK", "DEED", "DEEM", "DEER", "DEFT", "DEFY", "DELL", "DENT", "DENY", "DESK", "DIAL", "DICE",
            "DIED", "DIET", "DIME", "DINE", "DING", "DINT", "DIRE", "DIRT", "DISC", "DISH", "DISK", "DIVE", "DOCK", "DOES", "DOLE", "DOLL", "DOLT", "DOME",
            "DONE", "DOOM", "DOOR", "DORA", "DOSE", "DOTE", "DOUG", "DOUR", "DOVE", "DOWN", "DRAB", "DRAG", "DRAM", "DRAW", "DREW", "DRUB", "DRUG", "DRUM",
            "DUAL", "DUCK", "DUCT", "DUEL", "DUET", "DUKE", "DULL", "DUMB", "DUNE", "DUNK", "DUSK", "DUST", "DUTY", "EACH", "EARL", "EARN", "EASE", "EAST",
            "EASY", "EBEN", "ECHO", "EDDY", "EDEN", "EDGE", "EDGY", "EDIT", "EDNA", "EGAN", "ELAN", "ELBA", "ELLA", "ELSE", "EMIL", "EMIT", "EMMA", "ENDS",
            "ERIC", "EROS", "EVEN", "EVER", "EVIL", "EYED", "FACE", "FACT", "FADE", "FAIL", "FAIN", "FAIR", "FAKE", "FALL", "FAME", "FANG", "FARM", "FAST",
            "FATE", "FAWN", "FEAR", "FEAT", "FEED", "FEEL", "FEET", "FELL", "FELT", "FEND", "FERN", "FEST", "FEUD", "FIEF", "FIGS", "FILE", "FILL", "FILM",
            "FIND", "FINE", "FINK", "FIRE", "FIRM", "FISH", "FISK", "FIST", "FITS", "FIVE", "FLAG", "FLAK", "FLAM", "FLAT", "FLAW", "FLEA", "FLED", "FLEW",
            "FLIT", "FLOC", "FLOG", "FLOW", "FLUB", "FLUE", "FOAL", "FOAM", "FOGY", "FOIL", "FOLD", "FOLK", "FOND", "FONT", "FOOD", "FOOL", "FOOT", "FORD",
            "FORE", "FORK", "FORM", "FORT", "FOSS", "FOUL", "FOUR", "FOWL", "FRAU", "FRAY", "FRED", "FREE", "FRET", "FREY", "FROG", "FROM", "FUEL", "FULL",
            "FUME", "FUND", "FUNK", "FURY", "FUSE", "FUSS", "GAFF", "GAGE", "GAIL", "GAIN", "GAIT", "GALA", "GALE", "GALL", "GALT", "GAME", "GANG", "GARB",
            "GARY", "GASH", "GATE", "GAUL", "GAUR", "GAVE", "GAWK", "GEAR", "GELD", "GENE", "GENT", "GERM", "GETS", "GIBE", "GIFT", "GILD", "GILL", "GILT",
            "GINA", "GIRD", "GIRL", "GIST", "GIVE", "GLAD", "GLEE", "GLEN", "GLIB", "GLOB", "GLOM", "GLOW", "GLUE", "GLUM", "GLUT", "GOAD", "GOAL", "GOAT",
            "GOER", "GOES", "GOLD", "GOLF", "GONE", "GONG", "GOOD", "GOOF", "GORE", "GORY", "GOSH", "GOUT", "GOWN", "GRAB", "GRAD", "GRAY", "GREG", "GREW",
            "GREY", "GRID", "GRIM", "GRIN", "GRIT", "GROW", "GRUB", "GULF", "GULL", "GUNK", "GURU", "GUSH", "GUST", "GWEN", "GWYN", "HAAG", "HAAS", "HACK",
            "HAIL", "HAIR", "HALE", "HALF", "HALL", "HALO", "HALT", "HAND", "HANG", "HANK", "HANS", "HARD", "HARK", "HARM", "HART", "HASH", "HAST", "HATE",
            "HATH", "HAUL", "HAVE", "HAWK", "HAYS", "HEAD", "HEAL", "HEAR", "HEAT", "HEBE", "HECK", "HEED", "HEEL", "HEFT", "HELD", "HELL", "HELM", "HERB",
            "HERD", "HERE", "HERO", "HERS", "HESS", "HEWN", "HICK", "HIDE", "HIGH", "HIKE", "HILL", "HILT", "HIND", "HINT", "HIRE", "HISS", "HIVE", "HOBO",
            "HOCK", "HOFF", "HOLD", "HOLE", "HOLM", "HOLT", "HOME", "HONE", "HONK", "HOOD", "HOOF", "HOOK", "HOOT", "HORN", "HOSE", "HOST", "HOUR", "HOVE",
            "HOWE", "HOWL", "HOYT", "HUCK", "HUED", "HUFF", "HUGE", "HUGH", "HUGO", "HULK", "HULL", "HUNK", "HUNT", "HURD", "HURL", "HURT", "HUSH", "HYDE",
            "HYMN", "IBIS", "ICON", "IDEA", "IDLE", "IFFY", "INCA", "INCH", "INTO", "IONS", "IOTA", "IOWA", "IRIS", "IRMA", "IRON", "ISLE", "ITCH", "ITEM",
            "IVAN", "JACK", "JADE", "JAIL", "JAKE", "JANE", "JAVA", "JEAN", "JEFF", "JERK", "JESS", "JEST", "JIBE", "JILL", "JILT", "JIVE", "JOAN", "JOBS",
            "JOCK", "JOEL", "JOEY", "JOHN", "JOIN", "JOKE", "JOLT", "JOVE", "JUDD", "JUDE", "JUDO", "JUDY", "JUJU", "JUKE", "JULY", "JUNE", "JUNK", "JUNO",
            "JURY", "JUST", "JUTE", "KAHN", "KALE", "KANE", "KANT", "KARL", "KATE", "KEEL", "KEEN", "KENO", "KENT", "KERN", "KERR", "KEYS", "KICK", "KILL",
            "KIND", "KING", "KIRK", "KISS", "KITE", "KLAN", "KNEE", "KNEW", "KNIT", "KNOB", "KNOT", "KNOW", "KOCH", "KONG", "KUDO", "KURD", "KURT", "KYLE",
            "LACE", "LACK", "LACY", "LADY", "LAID", "LAIN", "LAIR", "LAKE", "LAMB", "LAME", "LAND", "LANE", "LANG", "LARD", "LARK", "LASS", "LAST", "LATE",
            "LAUD", "LAVA", "LAWN", "LAWS", "LAYS", "LEAD", "LEAF", "LEAK", "LEAN", "LEAR", "LEEK", "LEER", "LEFT", "LEND", "LENS", "LENT", "LEON", "LESK",
            "LESS", "LEST", "LETS", "LIAR", "LICE", "LICK", "LIED", "LIEN", "LIES", "LIEU", "LIFE", "LIFT", "LIKE", "LILA", "LILT", "LILY", "LIMA", "LIMB",
            "LIME", "LIND", "LINE", "LINK", "LINT", "LION", "LISA", "LIST", "LIVE", "LOAD", "LOAF", "LOAM", "LOAN", "LOCK", "LOFT", "LOGE", "LOIS", "LOLA",
            "LONE", "LONG", "LOOK", "LOON", "LOOT", "LORD", "LORE", "LOSE", "LOSS", "LOST", "LOUD", "LOVE", "LOWE", "LUCK", "LUCY", "LUGE", "LUKE", "LULU",
            "LUND", "LUNG", "LURA", "LURE", "LURK", "LUSH", "LUST", "LYLE", "LYNN", "LYON", "LYRA", "MACE", "MADE", "MAGI", "MAID", "MAIL", "MAIN", "MAKE",
            "MALE", "MALI", "MALL", "MALT", "MANA", "MANN", "MANY", "MARC", "MARE", "MARK", "MARS", "MART", "MARY", "MASH", "MASK", "MASS", "MAST", "MATE",
            "MATH", "MAUL", "MAYO", "MEAD", "MEAL", "MEAN", "MEAT", "MEEK", "MEET", "MELD", "MELT", "MEMO", "MEND", "MENU", "MERT", "MESH", "MESS", "MICE",
            "MIKE", "MILD", "MILE", "MILK", "MILL", "MILT", "MIMI", "MIND", "MINE", "MINI", "MINK", "MINT", "MIRE", "MISS", "MIST", "MITE", "MITT", "MOAN",
            "MOAT", "MOCK", "MODE", "MOLD", "MOLE", "MOLL", "MOLT", "MONA", "MONK", "MONT", "MOOD", "MOON", "MOOR", "MOOT", "MORE", "MORN", "MORT", "MOSS",
            "MOST", "MOTH", "MOVE", "MUCH", "MUCK", "MUDD", "MUFF", "MULE", "MULL", "MURK", "MUSH", "MUST", "MUTE", "MUTT", "MYRA", "MYTH", "NAGY", "NAIL",
            "NAIR", "NAME", "NARY", "NASH", "NAVE", "NAVY", "NEAL", "NEAR", "NEAT", "NECK", "NEED", "NEIL", "NELL", "NEON", "NERO", "NESS", "NEST", "NEWS",
            "NEWT", "NIBS", "NICE", "NICK", "NILE", "NINA", "NINE", "NOAH", "NODE", "NOEL", "NOLL", "NONE", "NOOK", "NOON", "NORM", "NOSE", "NOTE", "NOUN",
            "NOVA", "NUDE", "NULL", "NUMB", "OATH", "OBEY", "OBOE", "ODIN", "OHIO", "OILY", "OINT", "OKAY", "OLAF", "OLDY", "OLGA", "OLIN", "OMAN", "OMEN",
            "OMIT", "ONCE", "ONES", "ONLY", "ONTO", "ONUS", "ORAL", "ORGY", "OSLO", "OTIS", "OTTO", "OUCH", "OUST", "OUTS", "OVAL", "OVEN", "OVER", "OWLY",
            "OWNS", "QUAD", "QUIT", "QUOD", "RACE", "RACK", "RACY", "RAFT", "RAGE", "RAID", "RAIL", "RAIN", "RAKE", "RANK", "RANT", "RARE", "RASH", "RATE",
            "RAVE", "RAYS", "READ", "REAL", "REAM", "REAR", "RECK", "REED", "REEF", "REEK", "REEL", "REID", "REIN", "RENA", "REND", "RENT", "REST", "RICE",
            "RICH", "RICK", "RIDE", "RIFT", "RILL", "RIME", "RING", "RINK", "RISE", "RISK", "RITE", "ROAD", "ROAM", "ROAR", "ROBE", "ROCK", "RODE", "ROIL",
            "ROLL", "ROME", "ROOD", "ROOF", "ROOK", "ROOM", "ROOT", "ROSA", "ROSE", "ROSS", "ROSY", "ROTH", "ROUT", "ROVE", "ROWE", "ROWS", "RUBE", "RUBY",
            "RUDE", "RUDY", "RUIN", "RULE", "RUNG", "RUNS", "RUNT", "RUSE", "RUSH", "RUSK", "RUSS", "RUST", "RUTH", "SACK", "SAFE", "SAGE", "SAID", "SAIL",
            "SALE", "SALK", "SALT", "SAME", "SAND", "SANE", "SANG", "SANK", "SARA", "SAUL", "SAVE", "SAYS", "SCAN", "SCAR", "SCAT", "SCOT", "SEAL", "SEAM",
            "SEAR", "SEAT", "SEED", "SEEK", "SEEM", "SEEN", "SEES", "SELF", "SELL", "SEND", "SENT", "SETS", "SEWN", "SHAG", "SHAM", "SHAW", "SHAY", "SHED",
            "SHIM", "SHIN", "SHOD", "SHOE", "SHOT", "SHOW", "SHUN", "SHUT", "SICK", "SIDE", "SIFT", "SIGH", "SIGN", "SILK", "SILL", "SILO", "SILT", "SINE",
            "SING", "SINK", "SIRE", "SITE", "SITS", "SITU", "SKAT", "SKEW", "SKID", "SKIM", "SKIN", "SKIT", "SLAB", "SLAM", "SLAT", "SLAY", "SLED", "SLEW",
            "SLID", "SLIM", "SLIT", "SLOB", "SLOG", "SLOT", "SLOW", "SLUG", "SLUM", "SLUR", "SMOG", "SMUG", "SNAG", "SNOB", "SNOW", "SNUB", "SNUG", "SOAK",
            "SOAR", "SOCK", "SODA", "SOFA", "SOFT", "SOIL", "SOLD", "SOME", "SONG", "SOON", "SOOT", "SORE", "SORT", "SOUL", "SOUR", "SOWN", "STAB", "STAG",
            "STAN", "STAR", "STAY", "STEM", "STEW", "STIR", "STOW", "STUB", "STUN", "SUCH", "SUDS", "SUIT", "SULK", "SUMS", "SUNG", "SUNK", "SURE", "SURF",
            "SWAB", "SWAG", "SWAM", "SWAN", "SWAT", "SWAY", "SWIM", "SWUM", "TACK", "TACT", "TAIL", "TAKE", "TALE", "TALK", "TALL", "TANK", "TASK", "TATE",
            "TAUT", "TEAL", "TEAM", "TEAR", "TECH", "TEEM", "TEEN", "TEET", "TELL", "TEND", "TENT", "TERM", "TERN", "TESS", "TEST", "THAN", "THAT", "THEE",
            "THEM", "THEN", "THEY", "THIN", "THIS", "THUD", "THUG", "TICK", "TIDE", "TIDY", "TIED", "TIER", "TILE", "TILL", "TILT", "TIME", "TINA", "TINE",
            "TINT", "TINY", "TIRE", "TOAD", "TOGO", "TOIL", "TOLD", "TOLL", "TONE", "TONG", "TONY", "TOOK", "TOOL", "TOOT", "TORE", "TORN", "TOTE", "TOUR",
            "TOUT", "TOWN", "TRAG", "TRAM", "TRAY", "TREE", "TREK", "TRIG", "TRIM", "TRIO", "TROD", "TROT", "TROY", "TRUE", "TUBA", "TUBE", "TUCK", "TUFT",
            "TUNA", "TUNE", "TUNG", "TURF", "TURN", "TUSK", "TWIG", "TWIN", "TWIT", "ULAN", "UNIT", "URGE", "USED", "USER", "USES", "UTAH", "VAIL", "VAIN",
            "VALE", "VARY", "VASE", "VAST", "VEAL", "VEDA", "VEIL", "VEIN", "VEND", "VENT", "VERB", "VERY", "VETO", "VICE", "VIEW", "VINE", "VISE", "VOID",
            "VOLT", "VOTE", "WACK", "WADE", "WAGE", "WAIL", "WAIT", "WAKE", "WALE", "WALK", "WALL", "WALT", "WAND", "WANE", "WANG", "WANT", "WARD", "WARM",
            "WARN", "WART", "WASH", "WAST", "WATS", "WATT", "WAVE", "WAVY", "WAYS", "WEAK", "WEAL", "WEAN", "WEAR", "WEED", "WEEK", "WEIR", "WELD", "WELL",
            "WELT", "WENT", "WERE", "WERT", "WEST", "WHAM", "WHAT", "WHEE", "WHEN", "WHET", "WHOA", "WHOM", "WICK", "WIFE", "WILD", "WILL", "WIND", "WINE",
            "WING", "WINK", "WINO", "WIRE", "WISE", "WISH", "WITH", "WOLF", "WONT", "WOOD", "WOOL", "WORD", "WORE", "WORK", "WORM", "WORN", "WOVE", "WRIT",
            "WYNN", "YALE", "YANG", "YANK", "YARD", "YARN", "YAWL", "YAWN", "YEAH", "YEAR", "YELL", "YOGA", "YOKE"
        };


        /// <summary>
        /// Extract 'length' bits from the byte array 's' starting with bit 'start'
        /// </summary>
        private static int Extract(ReadOnlySpan<byte> s, int start, int length)
        {
            System.Diagnostics.Debug.Assert(length <= 11);
            System.Diagnostics.Debug.Assert(start >= 0);
            System.Diagnostics.Debug.Assert(length >= 0);
            System.Diagnostics.Debug.Assert(start + length <= 66);

            int startIndex = start / 8;
            var cl = s[startIndex];
            var cc = (startIndex + 1 < s.Length) ? s[startIndex + 1] : 0;
            var cr = (startIndex + 2 < s.Length) ? s[startIndex + 2] : 0;
            int x = (cl << 8 | cc) << 8 | cr;
            x >>= 24 - (length + (start % 8));
            x &= 0xFFFF >> (16 - length);
            return x;
        }

        private static void Insert(Span<byte> s, int start, int length, uint value)
        {
            System.Diagnostics.Debug.Assert(length <= 11);
            System.Diagnostics.Debug.Assert(start >= 0);
            System.Diagnostics.Debug.Assert(length >= 0);
            System.Diagnostics.Debug.Assert(start + length <= 66);

            int startIndex = start / 8;

            var shift = (8 - ((start + length) % 8)) % 8;
            var y = value << shift;
            var cl = (y >> 16) & 0xff;
            var cc = (y >> 8) & 0xff;
            var cr = y & 0xff;
            if (shift + length > 16)
            {
                s[startIndex] = (byte)(s[startIndex] | cl);
                s[startIndex + 1] = (byte)(s[startIndex + 1] | cc);
                s[startIndex + 2] = (byte)(s[startIndex + 2] | cr);
            }
            else if (shift + length > 8)
            {
                s[startIndex] = (byte)(s[startIndex] | cc);
                s[startIndex + 1] = (byte)(s[startIndex + 1] | cr);
            }
            else
            {
                s[startIndex] = (byte)(s[startIndex] | cr);
            }
        }

        /// <summary>
        /// Encode 8 bytes in 'bits' as a string of English words.
        /// </summary>
        /// <param name="bits">8 bytes to encode</param>
        /// <returns>The RFC1751 encoding of bits</returns>
        public static string Encode(ulong bits)
        {
            Span<byte> bytes = stackalloc byte[9]; // add in room for the parity 2 bits

            BinaryPrimitives.WriteUInt64LittleEndian(bytes, bits);
            /* compute parity */
            int p = 0;
            for (int i = 0; i < 64; i += 2)
                p += Extract(bytes, i, 2);
            bytes[8] = (byte)(p << 6);

            return string.Join(
                " ",
                words[Extract(bytes, 0, 11)],
                words[Extract(bytes, 11, 11)],
                words[Extract(bytes, 22, 11)],
                words[Extract(bytes, 33, 11)],
                words[Extract(bytes, 44, 11)],
                words[Extract(bytes, 55, 11)]);
        }

        public static string Encode(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length % 8 != 0)
            {
                throw new ArgumentException("RFC1751 works in 8 byte blocks", "buffer");
            }

            if (buffer.Length == 0)
            {
                return "";
            }

            var builder = new StringBuilder();
            for(int i = 0; i < buffer.Length; i += 8)
            {
                var bits = BinaryPrimitives.ReadUInt64LittleEndian(buffer.Slice(i, 8));
                builder.Append(Encode(bits));
                if (i + 8 < buffer.Length)
                {
                    builder.Append(" ");
                }
            }
            return builder.ToString();
        }

        private static uint Lookup(string word)
        {
            int index, length;
            if (word.Length < 4)
            {
                index = 0;
                length = 571;
            }
            else
            {
                index = 571;
                length = 1477;
            }
            var wordIndex = Array.BinarySearch(words, index, length, word.ToUpper());
            if (wordIndex == -1)
            {
                throw new ArgumentOutOfRangeException("word", word, "word is not in the RFC1751 dictionary");
            }
            return (uint)wordIndex;
        }

        public static ulong Decode(string word1, string word2, string word3, string word4, string word5, string word6, out bool parityError)
        {
            Span<byte> bytes = stackalloc byte[9]; // add in room for the parity 2 bits

            Insert(bytes, 0, 11, Lookup(word1));
            Insert(bytes, 11, 11, Lookup(word2));
            Insert(bytes, 22, 11, Lookup(word3));
            Insert(bytes, 33, 11, Lookup(word4));
            Insert(bytes, 44, 11, Lookup(word5));
            Insert(bytes, 55, 11, Lookup(word6));

            /* compute parity */
            int p = 0;
            for (int i = 0; i < 64; i += 2)
                p += Extract(bytes, i, 2);

            parityError = (p & 0x3) != (bytes[8] >> 6);

            return BinaryPrimitives.ReadUInt64LittleEndian(bytes);
        }

        public static ReadOnlyMemory<byte> Decode(string words, out bool parityError)
        {
            var split = words.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            if (split.Length % 6 != 0)
            {
                throw new ArgumentException("RFC1751 works in 6 word blocks", "words");
            }
            parityError = false;

            if (split.Length == 0)
            {
                return new byte[0];
            }

            var builder = new System.Buffers.ArrayBufferWriter<byte>(8);
            for (int i = 0; i < split.Length; i += 6)
            {
                var bits = Decode(split[i], split[i + 1], split[i + 2], split[i + 3], split[i + 4], split[i + 5], out var parity);
                parityError |= parity;

                var span = builder.GetSpan(8);
                BinaryPrimitives.WriteUInt64LittleEndian(span, bits);
                builder.Advance(8);
            }
            return builder.WrittenMemory;
        }
    }
}
