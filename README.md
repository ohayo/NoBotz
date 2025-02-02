# NoBotz
 NoBotz is a TShock plugin designed to enhance server security against Terraria bots and malicious clients. It helps prevent bot spam, automation of packets, and other exploitative behaviors. <br><br>
 🚨 **Warning**: NoBotz is **experimental** so **use** it **at your own risk**! 🚨 

 # ⚙️ Configuration ⚙️
 When you first run your server with NoBotz, a configuration file (NoBots.json) will be generated in the "tshock" folder. This file contains preset values for:
  - Packet rate limiting
  - Security thresholds
  - Other anti-bot mechanisms

**It is highly recommended that you adjust the values in NoBots.json to fit your server's needs. <br>
 There is no universal "sweet spot" for every server so tweak the limits and timeframes to minimize the rate of false positives.**

 If you only want notifications about suspicious activity without kicking players, set "KickOnTrip" to false. An option to temporarily ban the player from the server will be added soon.

 # 🚀 Features 🚀
 ✅ Packet rate limiting to prevent spam and bot abuse. <br>
 ✅ Detection of malicious client behavior to reduce exploits. <br>
 ✅ Configurable security settings for custom server protection. <br>
 ✅ Optional notification mode (disable auto-kicking if desired). <br>

 # 📜 License 📜
 This project is open-source and available under the GNU GPL v3 License.
