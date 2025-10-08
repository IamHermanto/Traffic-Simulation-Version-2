Building for Linux Server
1. Install Linux Build Support
Open Unity Hub → Installs → Click gear icon on your Unity version → Add Modules → Check "Linux Build Support (Mono)" → Install
2. Open Project
Unity Hub → Add → Select project folder → Open
3. Switch to Linux Platform
File → Build Settings → Select Linux → Switch Platform (wait for it to finish)
4. Build Settings

Target Platform: Linux
Architecture: x86_64

5. Player Settings
Click Player Settings:

Resolution and Presentation → Run In Background
Other Settings → Scripting Backend: Mono

6. Build
Click Build → Create folder named Server → Save as server.x86_64 → Wait
7. Upload to GitHub
bashcp -r Server/* /path/to/traffic-simulation-deployment/unity-build/
cd /path/to/traffic-simulation-deployment
git add unity-build/
git add -f unity-build/UnityPlayer.so
git commit -m "Update Unity build"
git push
8. Deploy
bashcd ~/traffic-system-ansible
ansible-playbook -i inventory.yml deploy.yml