import * as THREE from './three.module.js';
import { GLTFLoader } from './GLTFLoader.js';

const canvas = document.querySelector('#scene');
const renderer = new THREE.WebGLRenderer({ canvas, alpha: true, antialias: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 100);
// Center the cone vertically and leave room for its full silhouette while rotating.
camera.position.set(0, 0, 4.6);
scene.add(new THREE.HemisphereLight(0xfff4f7, 0x9a7482, 2.2));
const key = new THREE.DirectionalLight(0xffffff, 3.2);
key.position.set(-2, 4, 5);
scene.add(key);
const fill = new THREE.DirectionalLight(0xffc2d4, 1.4);
fill.position.set(3, 1, -3);
scene.add(fill);
let model = null;
let animationId = 0;
let stopped = false;
let readyNotified = false;
const clock = new THREE.Clock();

function resize() {
  const width = Math.max(1, canvas.clientWidth);
  const height = Math.max(1, canvas.clientHeight);
  renderer.setSize(width, height, false);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
}
new ResizeObserver(resize).observe(canvas);
resize();

new GLTFLoader().load('./ice_cream.glb', (gltf) => {
  const asset = gltf.scene;
  const bounds = new THREE.Box3().setFromObject(asset);
  const size = bounds.getSize(new THREE.Vector3());
  const center = bounds.getCenter(new THREE.Vector3());
  asset.position.sub(center);
  model = new THREE.Group();
  model.add(asset);
  model.scale.setScalar(1.9 / Math.max(size.x, size.y, size.z));
  scene.add(model);
}, undefined, (error) => window.chrome.webview.postMessage('error: ' + String(error)));

function animate() {
  if (stopped) return;
  animationId = requestAnimationFrame(animate);
  if (model) model.rotation.y += clock.getDelta() * (Math.PI * 2 / 5);
  renderer.render(scene, camera);
  if (model && !readyNotified) {
    readyNotified = true;
    window.chrome.webview.postMessage('model-ready');
  }
}
animate();

window.stopSplash = () => {
  stopped = true;
  cancelAnimationFrame(animationId);
  scene.traverse((object) => {
    if (object.geometry) object.geometry.dispose();
    if (object.material) {
      const materials = Array.isArray(object.material) ? object.material : [object.material];
      for (const material of materials) {
        for (const value of Object.values(material)) if (value?.isTexture) value.dispose();
        material.dispose();
      }
    }
  });
  renderer.dispose();
  renderer.forceContextLoss();
  window.removeEventListener('resize', resize);
};
