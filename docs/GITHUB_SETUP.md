# GitHub Setup

These steps create a clean Git repository from the current project folder.

## 1) Install Git LFS

Large Unity/mesh/data files are configured in `.gitattributes` for Git LFS.

```bash
git lfs install
```

## 2) Initialize The Repository

From the project root:

```bash
git init
git status
```

## 3) Add Files

The root `.gitignore` excludes generated Unity files, old archives, recordings, local `.env`, and old backend versions.

```bash
git add README.md docs .gitignore .gitattributes UnityApp ros_backend1.0
git status
```

Before committing, make sure these are not staged:

```text
UnityApp/Library/
UnityApp/Temp/
UnityApp/Builds/
UnityApp/*.csproj
UnityApp/*.sln
ros_backend1.0/build/
ros_backend1.0/install/
ros_backend1.0/log/
ros_backend1.0/.env
GripperCameraRecordings/
Archive/
Ros_archive/
ros_backend0.9/
```

## 4) Commit

```bash
git commit -m "Initial UR5e Hand-E VR teleop project"
```

## 5) Create GitHub Repo And Push

Create an empty GitHub repo, then connect it:

```bash
git branch -M main
git remote add origin git@github.com:<your-user>/<your-repo>.git
git push -u origin main
```

If you use HTTPS instead of SSH:

```bash
git remote add origin https://github.com/<your-user>/<your-repo>.git
git push -u origin main
```

## 6) Clone On Another Computer

```bash
git clone git@github.com:<your-user>/<your-repo>.git
cd <your-repo>
git lfs pull
cd ros_backend1.0
cp .env.example .env
./scripts/backend10_lifecycle.sh up_container
./scripts/backend10_lifecycle.sh build_ws
```
